{ pkgs ? import <nixpkgs> { } }:
let

in
pkgs.mkShell {
  # nativeBuildInputs is usually what you want -- tools you need to run
  nativeBuildInputs = with pkgs; [
    nixpkgs-fmt
    mono
    #dotnet-sdk
    #dotnet-runtime
    (with dotnetCorePackages; combinePackages [
          sdk_6_0
          runtime_6_0
        ])
    
    openssl
  ];
  
  shellHook = ''
    export LD_LIBRARY_PATH=${pkgs.openssl.out}/lib:$LD_LIBRARY_PATH
    export DOTNET_ROOT="${pkgs.dotnet-sdk}";
  '';
}
