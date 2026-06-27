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

La intrarea in DEC/OF, pentru o instructiune STORE (`ST (Rd), Rs1`):
- `A <- Rd` (registrul de adresa de baza)
- `C <- Rs1` (valoarea de stocat)

In stagiul MEM: `Memory.Write(MAR, C)` — fara hardware de redirectare suplimentar.

Alternativa (Varianta 1) ar fi necesitat un mux de redirectare `B <- Rd` in EX/MEM, mai complex de implementat.

### 2. Stagiul de rezolvare a branch-ului (§1.4)

**Implementat: rezolvare in EX** — penalitate de 2 cicli.

La sfarsitul stagiului EX:
- Se compara direct cei 2 registri din instructiunea de branch (ex. `BEQ R1,R2,L1` compara R1 cu R2).
- Se calculeaza `target = OriginAddress + InstructionSize + Offset`.
- Daca branch-ul este luat: `PC <- target`, stagiile IF si OF sunt flushed (NOP inserat).

Alternativa (rezolvare in WB) ar produce penalitate de 4 cicli.

### 3. Semantica branch-ului (§3.5)

**Implementata: branch RISC cu 2 registre** (Curs 2, slide 9).

Instructiunile `BEQ R1,R2,L1` / `BNE R1,R2,L1` / `BL R1,R2,L1` / `BGE R1,R2,L1` compara direct cei doi registri, fara registru de conditii (flag-uri). Nu exista `CMP` si nu exista flag-uri Z/C/S/O.

Branch-urile sunt instructiuni de 2 cuvinte: `OPCODE(8)|RS1(4)|RS2(4)` + `OFFSET(16)`.

### 4. Forwarding

Forwarding EX→OF si MEM→OF. Exceptie: instructiunile LD in stagiul EX (valoarea vine din MEM, nu EX). Load-use hazard produce exact 1 ciclu de stall, dupa care forwarding din MEM elimina stall-ul suplimentar. Prioritate: EX > MEM.

### 5. Registre

32 de registre generale R0–R31. R0 este hardwired la 0 (scrierile in R0 sunt ignorate). R31 este registrul link pentru `JAL` (adresa de retur).

### 6. Decizii pentru fazele ulterioare (de completat la implementare)

- **Varianta LRU exact (§8.2)**: TBD la Grupa 2.
- **Organizarea TLB (§9)**: TBD la Grupa 3.

---

## Pipeline pe 5 stagii

| # | Stagiu | Operatii |
|---|--------|----------|
| 1 | **IF** | `MAR <- PC`; fetch; `IR <- MDR`; `PC <- PC + N` |
| 2 | **DEC/OF** | Decodificare; `A <- Rs1`, `B <- Rs2`, `C <- Rd`(store); hazard; invalideaza Rd |
| 3 | **EX** | `C <- A op B` (ALU); `MAR <- A + offset` (LD/ST); compara registre pentru branch |
| 4 | **MEM** | Citire/scriere memorie date; pass-through ALU/branch |
| 5 | **WB** | `Rd <- C`; revalidare bit Rd; PC update daca branch luat |

---

## Sintaxa assembler (Faza 0)

```asm
; ALU registru-registru-registru
ADD R9, R8, R7       ; R9 = R8 + R7
SUB R1, R2, R3
AND R1, R2, R3
OR  R1, R2, R3
XOR R1, R2, R3

; ALU registru-registru-imediat (R-R-I)
ADD R1, R2, #10      ; R1 = R2 + 10
SUB R1, R2, #5

; Copiere registru (emulata cu ADD si R0)
ADD R5, R3, R0       ; R5 = R3 (MOV nu exista in RISC)

; LOAD (masina load-store)
LD R1, (R8)          ; R1 = Mem[R8]
LD R1, 16(R8)        ; R1 = Mem[R8 + 16]

; STORE
ST (R8), R1          ; Mem[R8] = R1
ST 16(R8), R1        ; Mem[R8 + 16] = R1

; Branch cu 2 registre (Curs 2, pag 9)
BEQ R1, R2, eticheta ; salt daca R1 == R2
BNE R1, R2, eticheta ; salt daca R1 != R2
BL  R1, R2, eticheta ; salt daca R1 < R2
BGE R1, R2, eticheta ; salt daca R1 >= R2

; Salt neconditionat
JMP 100h
JMP (R5)

; Apel subrutina (Jump And Link, salveaza PC+1 in R31)
JAL 200h
JAL (R5)

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
