#nullable enable
// WasmParser.cs contains a tiny ad hoc WebAssembly binary file parser used to:
// - Report the byte size of each section in a WebAssembly binary file.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Unity.Web.Stripping.Editor
{
    class WasmSectionSizes
    {
        public long Type;
        public long Import;
        public long Function;
        public long Table;
        public long Memory;
        public long Global;
        public long Export;
        public long Start;
        public long Element;
        public long Code;
        public long Data;
        public long DataCount;
        public long Tag;

        /// <summary>
        /// Custom sections (id=0) keyed by their name string, e.g. "name", ".debug_info", ".debug_line".
        /// The stored size is the full section payload length. When the same name appears multiple times
        /// (which the spec permits for custom sections), the sizes are accumulated.
        /// </summary>
        public readonly Dictionary<string, long> CustomSections = new();

        /// <summary>
        /// Reads size of custom section "name". This section is used by Emscripten to store function names.
        /// </summary>
        public long Name => CustomSections.GetValueOrDefault("name", 0L);

        /// <summary>
        /// Returns the overall size of all custom sections that contain DWARF debug information:
        /// .debug_loc, .debug_line, .debug_ranges, .debug_str, .debug_abbrev, .debug_info
        /// </summary>
        public long Dwarf
        {
            get
            {
                long dwarfSectionSize = 0;

                foreach (var dwarSection in DwarfSectionNames)
                {
                    dwarfSectionSize += CustomSections.GetValueOrDefault(dwarSection, 0L);
                }

                return dwarfSectionSize;
            }
        }

        private static readonly string[] DwarfSectionNames = new string[] { ".debug_loc", ".debug_line", ".debug_ranges", ".debug_str", ".debug_abbrev", ".debug_info" };
    }

    /// <summary>
    /// Enum for WebAssembly section IDs. See: https://webassembly.github.io/spec/core/binary/modules.html#binary-datacntsec
    /// for more information.
    /// </summary>
    enum WasmSectionId
    {
        Custom = 0,
        Type = 1,
        Import = 2,
        Function = 3,
        Table = 4,
        Memory = 5,
        Global = 6,
        Export = 7,
        Start = 8,
        Element = 9,
        Code = 10,
        Data = 11,
        DataCount = 12,
        Tag = 13,
    }

    /// <summary>
    /// A class to parse a WebAssembly file and gather basic information such as the size of sections.
    /// </summary>
    class WasmParser
    {
        /// <summary>
        /// Returns section sizes for the given .wasm file, or null if the file is not a valid WebAssembly binary
        /// or an I/O error occurs.
        /// </summary>
        /// <param name="filePath">Path to a WebAssembly file.</param>
        /// <returns>Size information for the individual sections.</returns>
        public static WasmSectionSizes? ParseSectionSizes(string filePath)
        {
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return ParseWasmSectionSizes(stream);
            }
            catch (Exception)
            {
                return null;
            }
        }

        static uint ReadLEB128(BinaryReader reader)
        {
            uint n = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                uint b = reader.ReadByte();
                n |= (b & 0x7Fu) << shift;
                if ((b & 0x80u) == 0) return n;
            }
            // LEB128 u32 should not be longer than 5 bytes
            throw new InvalidDataException("Invalid LEB128 sequence");
        }

        /// <summary>
        /// Magic number at start of wasm "\0asm"
        /// </summary>
        const uint k_WebAssemblyMagicNumber = 0x6D736100;

        /// <summary>
        /// Expected version number of WebAssembly file is 1.
        /// </summary>
        const uint k_WebAssemblyVersion = 1;

        static WasmSectionSizes? ParseWasmSectionSizes(Stream stream)
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Check header of WebAssembly file
            if (reader.ReadUInt32() != k_WebAssemblyMagicNumber ||
                reader.ReadUInt32() != k_WebAssemblyVersion)
                return null;

            // Parse sections and measure their size
            var sizes = new WasmSectionSizes();
            while (stream.Position < stream.Length)
            {
                var sectionId = (WasmSectionId)reader.ReadByte();
                uint sectionSize = ReadLEB128(reader);
                long sectionEnd = stream.Position + sectionSize;

                switch (sectionId)
                {
                    case WasmSectionId.Custom:
                        // Custom section: payload starts with a name string
                        uint nameLen = ReadLEB128(reader);
                        if (stream.Position + nameLen > sectionEnd)
                            throw new InvalidDataException("Custom section name length exceeds section boundary.");
                        string name = Encoding.UTF8.GetString(reader.ReadBytes((int)nameLen));

                        // There can be multiple sections with the same name.
                        // Accumulate the size of custom sections with same name.
                        sizes.CustomSections[name] = sizes.CustomSections.GetValueOrDefault(name, 0L) + sectionSize;
                        break;
                    case WasmSectionId.Type: sizes.Type = sectionSize; break;
                    case WasmSectionId.Import: sizes.Import = sectionSize; break;
                    case WasmSectionId.Function: sizes.Function = sectionSize; break;
                    case WasmSectionId.Table: sizes.Table = sectionSize; break;
                    case WasmSectionId.Memory: sizes.Memory = sectionSize; break;
                    case WasmSectionId.Global: sizes.Global = sectionSize; break;
                    case WasmSectionId.Export: sizes.Export = sectionSize; break;
                    case WasmSectionId.Start: sizes.Start = sectionSize; break;
                    case WasmSectionId.Element: sizes.Element = sectionSize; break;
                    case WasmSectionId.Code: sizes.Code = sectionSize; break;
                    case WasmSectionId.Data: sizes.Data = sectionSize; break;
                    case WasmSectionId.DataCount: sizes.DataCount = sectionSize; break;
                    case WasmSectionId.Tag: sizes.Tag = sectionSize; break;
                }

                if (sectionEnd > stream.Length)
                    throw new InvalidDataException("Section exceeds the end of the file.");
                stream.Seek(sectionEnd, SeekOrigin.Begin);
            }
            return sizes;
        }
    }
}