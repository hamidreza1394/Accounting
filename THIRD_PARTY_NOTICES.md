# Third-party software notices

## Microsoft TSS.MSR / Microsoft.TSS

- **Package:** Microsoft.TSS 2.1.1
- **Source:** https://github.com/microsoft/TSS.MSR
- **License:** MIT; full notice in [third_party/MICROSOFT-TSS-LICENSE.txt](third_party/MICROSOFT-TSS-LICENSE.txt).
- **Use:** TPM 2.0 command marshalling and transport abstraction. Windows uses TBS. Linux uses Faranou's strict local TPM device transport; the package's LinuxTpmDevice is intentionally not used because it can fall back to a localhost simulator.
- **Security boundary:** this package is not itself a trust anchor. Hardware assurance comes only from the provisioned TPM 2.0 NV Extend index and successful read-back verification. The runtime never creates, undefines or resets the index.
- **Review note:** the upstream TSS.Net implementation is a legacy .NET Standard library. Keep it isolated behind IOfflineRevocationRollbackAnchor, pin the exact package version, run compatibility/security review on upgrades, and verify against physical TPM hardware before Production use.
