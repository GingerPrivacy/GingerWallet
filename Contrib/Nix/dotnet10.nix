# Linux x64 SDK metadata from Microsoft's .NET 10 release manifest:
# https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json
# Keep the SDK version aligned with global.json.
{ buildAspNetCore, buildNetRuntime, buildNetSdk, fetchNupkg }:
let
  runtimePackage = pname: hash: fetchNupkg {
    inherit pname hash;
    version = "10.0.14";
  };
  commonPackages = [
    (runtimePackage "Microsoft.AspNetCore.App.Ref" "sha512-eUqw1tTYl50akv8nNFnUZvyHXsjlx6xd3uMN4V1pNou9b1vFq2+XcWUGvOlU2/sgzuKHtrEFiEQzxyFEpTmEnQ==")
    (runtimePackage "Microsoft.AspNetCore.App.Internal.Assets" "sha512-PzuvrER0jsiaaQejCMxdu9e+ROpi9aTrGQMLr0JQpNizYm55QzXNvBo+9r3+O5JNn5CyP847pWS9ScU5eig6qQ==")
    (runtimePackage "Microsoft.NETCore.DotNetAppHost" "sha512-tz3PuWZRJOQFeLhceC5wyMP6UO2YVDh5nPhD0F03b//m1n37fa6dSpDz5f7g5fNGNHyQ0RBZ8j+3UfV2WurkEg==")
    (runtimePackage "Microsoft.NETCore.App.Ref" "sha512-8xmkl8ngucEaOgvjRDIr3I4K2Xxx5K9ydiAWSs6fw5AX6MWkTBSF7cqYOrRWaLa0Bpj5EfJrPTSUqSLGgJANBA==")
    (runtimePackage "Microsoft.DotNet.ILCompiler" "sha512-obn/HAYT8K4e5aBYP25r2hlbFTdS58QMsUcwFEnxDIwmRJSeFao0aFGdmVbtrU1QIzSshSBXmqB+4625w7c28A==")
    (runtimePackage "Microsoft.NET.ILLink.Tasks" "sha512-YgO/Gkx7Ebb/0FIjQoNRdqn2e1IuzCZaiT3KzPkIIbTvQ17QRGfYNGBCDwkcMOtx2JOzWn0Kwqnuh6SdbSfZqw==")
  ];
  hostPackages.linux-x64 = [
    (runtimePackage "Microsoft.NETCore.App.Crossgen2.linux-x64" "sha512-EtXOHonxkJL6n0wkN2eQqrI7O4P19VF8HHGfT86XMZaEUgXYg6UftnJ/iXa3re3tE7sSOIF4x/Kg6hoaA4E02A==")
    (runtimePackage "runtime.linux-x64.Microsoft.DotNet.ILCompiler" "sha512-m/ieK6s5RbG2CPljI+m6HdDkFg72H/ZSlanRER4btNulnlUvhF+/KJuVZOLM/K5IySJ+RiG6aR8In0uiOoWqig==")
  ];
  targetPackages.linux-x64 = [
    (runtimePackage "Microsoft.AspNetCore.App.Runtime.linux-x64" "sha512-Cp+0XHjY+V7Ac2boAkKymKHAAvmpitw231tj4LufSuSlKTeMTRBQvjkKXR1ahkY/x+eCJQu3TDIGNmOgElkU0A==")
    (runtimePackage "Microsoft.NETCore.App.Host.linux-x64" "sha512-LqyCvEWkMshyGeeZ1y1KraQ6czMsISLmMB5VKDT4addsZysxjlMC7WbCryVokdAO7juS8htBOZ3+KaDY5FQ6FA==")
    (runtimePackage "Microsoft.NETCore.App.Runtime.linux-x64" "sha512-IfDwQKE3gcK9u7iU7C/bOaqLNfoRf6zvXzGLHsJAbsC7Shwn5ztRCRWjD1mKRk5XUrPO9VWSC1+q8oimXRDlsg==")
    (runtimePackage "runtime.linux-x64.Microsoft.NETCore.DotNetAppHost" "sha512-WdN/aKRDOjgKZZ1ZF9HAOTZM46rJYElLXoI4IbEQiJfNdAqFZYiXZ3mdBcgOmLuf5AVTeUg3ILxgkyE3VBDf1Q==")
    (runtimePackage "Microsoft.NETCore.App.Runtime.NativeAOT.linux-x64" "sha512-IIj3QW0MZPRI7NOKO44MpNCMcgLo9+z9/CqWVmtejV735Aap2/cheQ7yGZS28H93+yu4LEKWy1SBcLi5Y4+DHQ==")
  ];
in rec {
  aspnetcore_10_0 = buildAspNetCore {
    version = "10.0.14";
    srcs.linux-x64 = {
      url = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.14/aspnetcore-runtime-10.0.14-linux-x64.tar.gz";
      hash = "sha512-3QJP0mFpJqVVt1y/eoNvbc6ZgZnwWW2ddWdtyiudWcjuhnPGrCNHPA8aS91Z61zQL+mrCX7q8t2Mi9EXyF5wMQ==";
    };
  };
  runtime_10_0 = buildNetRuntime {
    version = "10.0.14";
    srcs.linux-x64 = {
      url = "https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.14/dotnet-runtime-10.0.14-linux-x64.tar.gz";
      hash = "sha512-Y0MmAoZF84cxyMTnGmg1bkyg1rZgcTmiNKt2Bof3QgY+2vM6HyDdphV0LS3IMAyi/4V2GdUfuzgJA4O3EzSviQ==";
    };
  };
  sdk_10_0 = buildNetSdk {
    version = "10.0.403";
    srcs.linux-x64 = {
      url = "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz";
      hash = "sha512-t7PYRhz5iwfSVSzpGZcC+jrQEKwmcaX4zVE291NOUPaFW6e7sCsRSUVj8dBBVidqMc2hEDuQDl7s0NlHPce5xQ==";
    };
    inherit commonPackages hostPackages targetPackages;
    runtime = runtime_10_0;
    aspnetcore = aspnetcore_10_0;
  };
}

