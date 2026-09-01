{
  description = "Dev shell with a Linux-native .NET SDK for csharp-ls (LSP tooling only)";

  inputs = {
    nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
    flake-utils.url = "github:numtide/flake-utils";
  };

  outputs = { self, nixpkgs, flake-utils }:
    flake-utils.lib.eachDefaultSystem (system:
      let
        pkgs = import nixpkgs { inherit system; };

        # csharp-ls 0.26.0 wants net10.0. Keep 9.0 alongside as a fallback
        # in case sdk_10_0 fails to build on your nixpkgs revision.
        dotnetPkg = pkgs.dotnetCorePackages.combinePackages [
          pkgs.dotnetCorePackages.sdk_10_0
          pkgs.dotnetCorePackages.sdk_9_0
        ];
      in
      {
        devShells.default = pkgs.mkShell {
          name = "csharp-lsp-shell";

          packages = [
            dotnetPkg
            pkgs.csharp-ls
            pkgs.nodejs_22
          ];

          # Common native deps dotnet sometimes wants at runtime (globalization, TLS)
          buildInputs = [
            pkgs.icu
            pkgs.openssl
            pkgs.zlib
          ];

          shellHook = ''
            export DOTNET_ROOT="${dotnetPkg}"
            export PATH="${dotnetPkg}/bin:$PATH"

            # Keep dotnet from trying to write telemetry/first-run junk into
            # the read-only nix store path
            export DOTNET_CLI_TELEMETRY_OPTOUT=1
            export DOTNET_NOLOGO=1
            export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

            echo "dotnet: $(dotnet --version 2>/dev/null || echo 'FAILED TO RUN')"
            echo "DOTNET_ROOT=$DOTNET_ROOT"
          '';
        };
      });
}
