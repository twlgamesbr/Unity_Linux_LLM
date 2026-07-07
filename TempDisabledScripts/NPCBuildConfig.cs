namespace NPCSystem.Editor
{
    static class NPCBuildConfig
    {
        [UserSetting("Output Paths", "Server Executable")]
        public static readonly NPCBuildSetting<string> serverOutputPath = new(
            "output.serverPath",
            "Builds/Server/NPCServer.x86_64"
        );

        [UserSetting("Output Paths", "Client Executable")]
        public static readonly NPCBuildSetting<string> clientOutputPath = new(
            "output.clientPath",
            "Builds/Client/NPCClient.x86_64"
        );

        [UserSetting("Output Paths", "WebGL Output Directory")]
        public static readonly NPCBuildSetting<string> webglOutputDir = new(
            "output.webglDir",
            "Builds/WebGL_client/LinuxWebGLWS"
        );

        [UserSetting("Docker", "WebGL Container Name")]
        public static readonly NPCBuildSetting<string> webglContainer = new(
            "docker.webglContainer",
            "npc-webgl-client"
        );

        [UserSetting("Docker", "Server Container Name")]
        public static readonly NPCBuildSetting<string> serverContainer = new(
            "docker.serverContainer",
            "npc-dedicated-server"
        );

        [UserSetting("Docker", "Compose Directory")]
        public static readonly NPCBuildSetting<string> dockerComposeDir = new(
            "docker.composeDir",
            "docker_webgl_client"
        );

        [UserSetting("WebGL", "Chmod Mode")]
        public static readonly NPCBuildSetting<string> webglChmodMode = new(
            "webgl.chmodMode",
            "a+rX"
        );

        [UserSetting("WebGL", "Nginx Port")]
        public static readonly NPCBuildSetting<int> webglPort = new("webgl.port", 8085);

        [UserSetting("Server", "Port")]
        public static readonly NPCBuildSetting<int> serverPort = new("server.port", 11474);

        [UserSetting("Server", "Use WebSockets")]
        public static readonly NPCBuildSetting<bool> serverUseWebSockets = new(
            "server.useWebSockets",
            true
        );
    }
}
