# Simulator RISC Pipeline — Arhitectura Calculatoarelor

Simulator interactiv pas-cu-pas al unui procesor RISC cu pipeline pe 5 stagii, implementat în C# (.NET 9), WPF, MVVM.

---

## Structura proiectului

```
RiscEmulator.sln
├── RiscEmulator.Logic/     — logica pura (fara dependente de UI)
├── RiscEmulator.UI/        — interfata WPF, pattern MVVM
└── RiscEmulator.Tests/     — teste xUnit pentru Logic
```

---

## Decizii de design (§14.2 din PROJECT_PLAN.md)

### 1. Varianta de store (§1.4)

**Implementata: Varianta 2** — RegisterFile cu 3 porturi de citire.

La intrarea in DEC/OF, pentru o instructiune STORE (`MOV (Rd), Rs1`):
- `A <- Rd` (registrul de adresa de baza)
- `C <- Rs1` (valoarea de stocat)

In stagiul MEM: `Memory.Write(MAR, C)` — fara hardware de redirectare suplimentar.

Alternativa (Varianta 1) ar fi necesitat un mux de redirectare `B <- Rd` in EX/MEM, mai complex de implementat.

### 2. Stagiul de rezolvare a branch-ului (§1.4)

**Implementat: rezolvare in EX** — penalitate de 2 cicli.

La sfarsitul stagiului EX:
- Se evalueaza conditia pe baza flag-urilor setate de CMP anterior.
- Se calculeaza `ALUR = OriginAddress + 1 + Offset`.
- Daca branch-ul este luat: `PC <- ALUR`, stagiile IF si OF sunt flushed (NOP inserat).

Alternativa (rezolvare in WB) ar produce penalitate de 4 cicli.

### 3. Semantica branch-ului (§3.5)

**Implementata: branch pe flag-uri + CMP.**

Instructiunile `BEQ`/`BNE`/`BPL`/`BMI`/`BCS`/`BCC`/`BVS`/`BVC` verifica flag-urile Z/C/S/O setate de `CMP Rs1, Rs2` anterior.

Motivul: formatul Class 3 este `OPCODE(8) | OFFSET(8)` = 16 biti — nu exista spatiu pentru 2 campuri de registru suplimentare (varianta "RISC pura" BEQ Ri,Rj,L1 nu e aplicabila acestui format).

### 4. Forwarding

Forwarding EX->OF si MEM->OF. Exceptie: instructiunile LOAD in stagiul EX (valoarea vine din MEM, nu EX). Load-use hazard produce exact 1 ciclu de stall, dupa care forwarding din MEM elimina stall-ul suplimentar. Prioritate: EX > MEM.

### 5. Decizii pentru fazele ulterioare (de completat la implementare)

- **Varianta LRU exact (§8.2)**: TBD la Grupa 2.
- **Organizarea TLB (§9)**: TBD la Grupa 3.

---

## Pipeline pe 5 stagii

| # | Stagiu | Operatii |
|---|--------|----------|
| 1 | **IF** | `MAR <- PC`; fetch; `IR <- MDR`; `PC <- PC + N` |
| 2 | **DEC/OF** | Decodificare; `A <- Rs1`, `B <- Rs2`, `C <- Rd`(store); hazard; invalideaza Rd |
| 3 | **EX** | `C <- A op B` (ALU); `MAR <- A + offset` (load/store); conditie branch |
| 4 | **MEM** | Citire/scriere memorie date; pass-through ALU/branch |
| 5 | **WB** | `Rd <- C`; revalidare bit Rd; PC update daca branch luat |

---

## Sintaxa assembler (Faza 0)

```asm
; ALU
ADD R9, R8, R7       ; R9 = R8 + R7
SUB R1, R2, R3

; LOAD
MOV R1, (R8)         ; R1 = Mem[R8]
MOV R1, 16(R8)       ; R1 = Mem[R8 + 16]

; STORE
MOV (R8), R1         ; Mem[R8] = R1
MOV 16(R8), R1

; Compare + branch
CMP R1, R2           ; seteaza flags Z/C/S/O
BEQ eticheta
BNE eticheta

; Jump
JMP 100h
JMP (R5)

; Diverse
NOP
HALT
```

Comentarii cu `;`. Etichete cu `:`.

---

## Rulare

```
dotnet run --project RiscEmulator.UI
dotnet test RiscEmulator.Tests
```
