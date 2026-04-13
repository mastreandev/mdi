# CRC-16/XMODEM

This page contains the CRC-16/XMODEM entry from the RevEng CRC catalogue, which matches the repository implementation in `source/MDI/IO/Hashing/Crc16.cs`.

## Parameters

```text
width=16
poly=0x1021
init=0x0000
refin=false
refout=false
xorout=0x0000
check=0x31c3
residue=0x0000
name="CRC-16/XMODEM"
```

## Description

- Class: attested
- Alias: `CRC-16/ACORN`, `CRC-16/LTE`, `CRC-16/V-41-MSB`, `XMODEM`, `ZMODEM`
- MSB-first form of the V.41 algorithm
- CRC presented high byte first
- Used in the MultiMediaCard interface
- In XMODEM and Acorn MOS, message bits are processed out of transmission order, which compromises some burst error detection guarantees

## Standards and references

- **ITU-T Recommendation V.41** (November 1988)
  - Definition: Residue; full mathematical description (Section 2, p.2)
  - Shift register diagrams (Appendix I, p.9)
- **3GPP TS 36.212 v17.1.0 / ETSI TS 136 212 v17.1.0**
  - Definition: Width, Poly, Init, XorOut, Residue (Section 5.1.1, pp.10–11)
  - Attachment relation defining `RefIn ^ RefOut` (Section 5.1.1, p.11)
- **3GPP TS 36.321 v17.5.0 / ETSI TS 136 321 v17.5.0**
  - Definition: RefIn, RefOut (Section 6.1.1, p.90)
- **JEDEC Standard JESD84-A441** (March 2010)
  - Full definition (Section 10.2, pp.157–8)
  - Shift register diagram (Figure 54, p.159)
- **Acorn Computers Ltd** (October 1984), _BBC Microcomputer User Guide_
  - Pseudocode (Chapter 35, p.369)

## Implementations

- Ward Christensen, Keith Petersen et al. (8 June 1982), XMODEM 5.0
- Acorn Computers Ltd (1981), _Acorn MOS 1.20_ (BBC Micro cassette format)
- Lammert Bies (August 2011), CRC calculator
- PVL Team (25 October 2008), CRC .NET control, version 14.0.0.0
- Berndt M. Gammel (29 October 2006), Matpack 1.9.1 class MpCRC documentation
- Altera Corporation (April 1999), _crc MegaCore Function Data Sheet_, version 2
  - All parameters except Residue cited for ZMODEM (p.6)
- William H. Press, Brian P. Flannery, Saul A. Teukolsky, William T. Vetterling (1992), _Numerical Recipes in C_, 2nd ed.
  - All parameters except Check (p.898)
  - Code: C (pp.900–1)
  - 2 codewords (p.898)

## Codewords

- `541A71`
- `4361744D 6F757365 39383736 35343332 31E556`

---

_Source and attribution_: [RevEng CRC Catalogue](https://reveng.sourceforge.io/crc-catalogue/16.htm#crc.cat.crc-16-xmodem)
