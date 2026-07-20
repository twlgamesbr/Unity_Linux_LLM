namespace Unity.Numerics.Linear.Dense.Primitives
{
    internal struct Assertions
    {
        public static void AssertCompatibleDimensions(int n, int m)
        {
            UnityEngine.Debug.Assert(n == m, "Vectors must have the same size");
        }

        public static void AssertCompatibleDimensions(in Vector a, in Vector b)
        {
            AssertCompatibleDimensions(a.Dimension, b.Dimension);
        }

        public static void AssertCompatibleDimensions(in Matrix m, in Vector a)
        {
            AssertCompatibleDimensions(m.NumCols, a.Dimension);
        }

        public static void AssertCompatibleDimensionsTranspose(in Matrix m, in Vector a)
        {
            AssertCompatibleDimensions(m.NumRows, a.Dimension);
        }
    }
}
