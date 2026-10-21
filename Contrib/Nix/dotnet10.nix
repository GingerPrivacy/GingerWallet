# Linux x64 SDK metadata from Microsoft's .NET 10 release manifest:
# https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json
# Keep the SDK version aligned with global.json.
{ buildAspNetCore, buildNetRuntime, buildNetSdk, fetchNupkg }:
let
  runtimePackage = pname: hash: fetchNupkg {
    inherit pname hash;
    version = "10.0.13";
  };
  commonPackages = [
    (runtimePackage "Microsoft.AspNetCore.App.Ref" "sha512-GIIEHQIsQohxLcoFf/OJglifzh33VDyJXRR8V5q1HupJhmV6GkIvWbZgoV97nZGw07UYaDjgsDLqYQ2lFkyM5A==")
    (runtimePackage "Microsoft.AspNetCore.App.Internal.Assets" "sha512-/ds8Tnf03b23EVRv37C6iOXVleTn8zpNVYeNayDEfUQ2WAyoaulbbDYT24nCwSPXXqu9dGXR06gbISt329JOjA==")
    (runtimePackage "Microsoft.NETCore.DotNetAppHost" "sha512-82kRjGFuI7oE0u1WWeAhku2t7mRcBQSVPFU1jVTlC0S+wBtfKVcnP5ylUnELTLDYqZcdjYu9IwVXfkvL6cZAVw==")
    (runtimePackage "Microsoft.NETCore.App.Ref" "sha512-VE8mNXuWljFOtfjF58uHz+56F2sC7ahlco8H9/Xu6We0+kRsQkVBJi7H9RlmMxx4cASyJfydSeNku7Q97MaV0A==")
    (runtimePackage "Microsoft.DotNet.ILCompiler" "sha512-N0hYFEX3bcXO27yi2yJueXNm1ziCqjMSEAbbZwIvPR82tQjPVM8n2IpLED1Jq7ut8ZJlBvTUbn1JK+KYhILlQw==")
    (runtimePackage "Microsoft.NET.ILLink.Tasks" "sha512-0X3X3VG/W+UmO9EiS88KlEQ8wppRe0bctvc5b6qu8tFBxRFfg/NBwriNeR7LMYa0AzNachpvtmkXHE6WyI5zJg==")
  ];
  hostPackages.linux-x64 = [
    (runtimePackage "Microsoft.NETCore.App.Crossgen2.linux-x64" "sha512-xCcpVW3jRLfLScyeXIDiJSqpFI2r5+a8SEcjfaH8Htig1CH80Uk+h+0edGvPLaGEgwjJ/70mAuIhD+WXteH79Q==")
    (runtimePackage "runtime.linux-x64.Microsoft.DotNet.ILCompiler" "sha512-z5EQqXnArBulOqhgGZ5OlTknvQbLyuXCXNFYDKI9nswtixz24WvG5KqOxkw59pwTYiZ4Q/7aYQnvFq2SDQMcaw==")
  ];
  targetPackages.linux-x64 = [
    (runtimePackage "Microsoft.AspNetCore.App.Runtime.linux-x64" "sha512-0/onL7C/TK+A0kkCqMHWU7Ek9Y5xTgzL+Iu3f796T7tGSU+nCNELuAqvgf96Bz+eqE5hWZExam9QHQ1pQhBkIA==")
    (runtimePackage "Microsoft.NETCore.App.Host.linux-x64" "sha512-/1GyFapjXBxLallg154odqJBX+H/Y16zeM8S177ERwlBbExuKxhcpqgnFtvOM9y/Dv+j+q7VCcmygZghPtIf/g==")
    (runtimePackage "Microsoft.NETCore.App.Runtime.linux-x64" "sha512-/REkglvNhkHTaYq+6/YVY2rjleCzgNhgJ2/7bEvpy+38SyktZh19QmNm0261frVAGv2OWM5CtxO9rK1R6JYx8g==")
    (runtimePackage "runtime.linux-x64.Microsoft.NETCore.DotNetAppHost" "sha512-UtomKpAPXuzPVM6axmBtkimSnOw1XY2fMTp1dUutLZ7XRZKHYIGSyAQLhy9Z85HLwifoY+w1zmN1UPLAGm+slQ==")
    (runtimePackage "Microsoft.NETCore.App.Runtime.NativeAOT.linux-x64" "sha512-sz7bDdmunUmbyKvp6++QQeXRWWZLmNWlGtImV9hK1bDK1sISmNzUsy2yNvtgT6nc0PJ/lYQyhR+u8HhXP8NIIA==")
  ];
in rec {
  aspnetcore_10_0 = buildAspNetCore {
    version = "10.0.13";
    srcs.linux-x64 = {
      url = "https://builds.dotnet.microsoft.com/dotnet/aspnetcore/Runtime/10.0.13/aspnetcore-runtime-10.0.13-linux-x64.tar.gz";
      hash = "sha512-eyDUFk8Jfhz2FtgXS2pkvhAObzDcTgKLFuVR6LDIbf8O9CS7S/UtH/DsyjjhAtP6gIFOBsuFoy1u7kw7mXZPeQ==";
    };
  };
  runtime_10_0 = buildNetRuntime {
    version = "10.0.13";
    srcs.linux-x64 = {
      url = "https://builds.dotnet.microsoft.com/dotnet/Runtime/10.0.13/dotnet-runtime-10.0.13-linux-x64.tar.gz";
      hash = "sha512-lXTZU2QRBAlBZzXnhCeW+NYUwh10yyXSBKPkz9EbLAXv3xnKibgGllCgT2St0nBZZ9ywS4Zlo216PzCl2MBa/g==";
    };
  };
  sdk_10_0 = buildNetSdk {
    version = "10.0.402";
    srcs.linux-x64 = {
      url = "https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.402/dotnet-sdk-10.0.402-linux-x64.tar.gz";
      hash = "sha512-GQSpjdots0uni6PE+xsiRFUymvkcNB2DVHhDwYOeESsErmme6S+iRJqPYiSRlqk4cjB4wMYLn2XIhTnVaHcpwg==";
    };
    inherit commonPackages hostPackages targetPackages;
    runtime = runtime_10_0;
    aspnetcore = aspnetcore_10_0;
  };
}
