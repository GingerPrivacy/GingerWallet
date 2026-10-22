<p align="center">
<img width="1486" height="853" alt="main" src="https://github.com/user-attachments/assets/5a0b803c-80c3-439b-8f78-6f33da90a45f" />
</p>

<h1 align="center">Ginger Wallet</h1>

<p align="center">
  Privacy-focused Bitcoin wallet.
</p>

<p align="center">
  <a href="https://github.com/GingerPrivacy/GingerWallet/actions/workflows/UnitTests.yml">
    <img src="https://github.com/GingerPrivacy/GingerWallet/actions/workflows/UnitTests.yml/badge.svg?branch=master" alt="UnitTests" />
  </a>
</p>

<h3 align="center">
  <a href="https://gingerwallet.io">Website</a>
  <span> | </span>
  <a href="https://docs.gingerwallet.io/">Documentation</a>
  <span> | </span>
  <a href="https://api.gingerwallet.io/swagger">API</a>
  <span> | </span>
  <a href="https://github.com/GingerPrivacy/GingerWallet/discussions/5185">Support</a>
  <span> | </span>
  <a href="https://github.com/GingerPrivacy/GingerWallet/blob/master/PGP.txt">PGP</a>
</h3>

---

## What is Ginger Wallet?

Ginger Wallet is a privacy-focused, open-source Bitcoin desktop wallet.

The goal is to provide a reliable and privacy-aware user experience while keeping the full source code publicly available.

---

## Downloads

Official release binaries are available in the GitHub **Releases** section.

There are no pre-releases.  
You can always build the latest version directly from the `master` branch.

---

## Release Artifacts

- **MSI** Windows – Code signed (Martin Rimoczi, Certum Code Signing CA)
- **DMG** MacOS x64 and ARM64 versions – Signed with Apple Developer ID
- **ZIP (portable)** – Binaries for all platforms, Deterministically reproducible build
- **DEB** - Debian linux package
- **tar.gz** - Compressed archive containing the Linux application files  


### Verification
- All files include a corresponding **PGP (.asc)** signature  
- Public key: <https://github.com/GingerPrivacy/GingerWallet/blob/master/PGP.txt>

---

## Build from source (master)

### Requirements
- .NET 10 SDK (the version pinned in `global.json`)
- Git

### Quick start

```bash
git clone https://github.com/GingerPrivacy/GingerWallet.git
cd GingerWallet
dotnet restore --locked-mode
dotnet build --configuration Release
dotnet run --configuration Release
```

To run unit tests:

```bash
dotnet test --configuration Release --filter "UnitTests"
```

---

## Contributing

See:  
https://github.com/GingerPrivacy/GingerWallet/blob/master/CONTRIBUTING.md

---

## Security

If you believe you found a security issue, avoid opening a public issue.  
Use GitHub Security Advisories (if enabled) or contact the maintainers privately.

---

## License

See the `LICENSE` file in this repository.
