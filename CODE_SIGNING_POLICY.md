# Code signing policy / Política de assinatura de código

Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org).
(Assinatura de código gratuita fornecida pela SignPath.io, certificado da SignPath Foundation.)

> Status: application to SignPath Foundation pending. Until it is approved, releases are **not signed**. Verify them with the SHA-256 and the build attestation (see README).

## What is signed / O que é assinado

Only `TapGlobalPatcher.exe`, built by the GitHub Actions workflow `.github/workflows/build.yml` directly from the source code in this repository. Nothing is built or signed on a personal computer.

Somente o `TapGlobalPatcher.exe`, compilado pelo workflow `.github/workflows/build.yml` diretamente a partir do código deste repositório.

## Team roles / Papéis

| Role | Members |
|---|---|
| Committers and reviewers | [dkzT](https://github.com/dkzT) |
| Approvers (release signing) | [dkzT](https://github.com/dkzT) |

All members use multi-factor authentication on GitHub and SignPath.

## Privacy / Privacidade

This program does not connect to the network and does not collect or transmit any data. It only reads the firmware file you select and writes the new file where you choose.

Este programa não acessa a internet e não coleta nem envia dados. Ele só lê o arquivo de firmware que você escolhe e grava o novo arquivo onde você indicar.

## Disclaimer

Independent project, not affiliated with Valeton. The Valeton firmware is not distributed here: the tool modifies the official file that the user downloads from Valeton.
