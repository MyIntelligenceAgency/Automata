# Baseline .NET 8.0 — Automata.Benchmarks

Mesures de référence de l'API publique d'Automata sur .NET 8, point de départ
des comparaisons .NET 11 (EPIC gains runtime — CoursIA issues 18770 et 18695,
digest Stephen Toub 2026-09-15). Sœur du baseline MetaGeneticSharp
(.NET 9, 2026-10-02) : même protocole, même toolchain.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-10-14 |
| OS | Windows 11 Pro (10.0.26300.9457) |
| CPU | 12th Gen Intel Core i7-12700H |
| Runtime | .NET 8.0.32 (8.0.3226.50405), X64 RyuJIT AVX2 |
| SDK (build) | 10.0.113 |
| BenchmarkDotNet | 0.14.0 |
| Toolchain | InProcessEmitToolchain, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations |
| Machine | poste de travail portable (laptop), alimentation secteur |

## Charges mesurées

Toutes passent par l'API publique (`Automaton<BDD>`, `CharSetSolver`) — aucune
I/O, aucun réseau, structure **figée** (aucun RNG : mêmes opérandes à chaque
itération). Charge commune : un epsilon-NFA en 15 couches, chacune 3 branches
convergentes vers le pivot suivant (61 états / 90 transitions, 5 classes de
caractères BDD rotatives + boucle par branche) :

| Benchmark | Charge |
| --- | --- |
| `Determinize_NFA_L15` (baseline) | déterminisation seule du NFA (PowerSetStateBuilder) |
| `Determinize_plus_MinimizeHopcroft_L15` | même charge + minimisation Hopcroft du DFA obtenu |
| `IsEquivalent_DFA_Twins_L15` | équivalence de 2 DFA de même langage, numérotations d'états inversées (2 déterminisations + exploration croisée) |

Les charges reconstruisent l'automate à chaque invocation : le coût de
construction est identique sur tous les runtimes et fait partie de la charge
commune (les 3 colonnes le partagent).

## Résultats (job InProcessEmitToolchain — référence)

| Method | Mean | Error | StdDev | Ratio | Gen0 | Gen1 | Gen2 | Allocated | Alloc Ratio |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Determinize_NFA_L15 | 25.043 ms | 3.075 ms | 2.8766 ms | 1.01 | - | - | - | 11.42 MB | 1.00 |
| Determinize_plus_MinimizeHopcroft_L15 | 71.566 ms | 20.590 ms | 19.2603 ms | 2.89 | 2000.0000 | 1000.0000 | 1000.0000 | 26.05 MB | 2.28 |
| IsEquivalent_DFA_Twins_L15 | 24.935 ms | 1.576 ms | 1.3970 ms | 1.01 | 1000.0000 | - | - | 24.08 MB | 2.11 |

Lecture hot-path (axes Toub visés) :

- **Déterminisation** : 11,42 MB pour un NFA de 61 états — le
  PowerSetStateBuilder alloue ses ensembles-états (HashSet/Dictionary) à chaque
  sous-ensemble exploré. Levier principal : allocations/write barriers moins
  chères.
- **Minimisation Hopcroft** : ajoute ~46 ms et +14,6 MB (2,28× l'alloc de la
  déterminisation seule), GC Gen0/Gen1/Gen2 = 2000/1000/1000 pour 1000
  opérations — la charge la plus GC-bound du banc (raffinement de partition,
  `Dictionary.Remove` / HashSet churn).
- **Équivalence** : même temps que la déterminisation seule (ratio 1,01 —
  2 déterminisations + exploration croisée amorties) mais 24,08 MB (2,11×) :
  l'exploration paie en allocations, pas en temps.
- **Multimodalité** : `Determinize_NFA_L15` présente plusieurs modes (mValue=3)
  sous ShortRun ; la comparaison .NET 11 devra rejouer le même protocole et ne
  pas trancher sur un unique Mean.

## Protocole / reproduction

```bash
cd src/Automata.Benchmarks
dotnet run -c Release -f net8.0 -- --filter '*'
```

- Projet unique `src/Automata.Benchmarks` (multi-cible `net8.0;net9.0`) : la
  suite `AutomatonOpsBench` a été portée ici sur arbitrage ai-01 (c2189) — le
  second projet `benchmarks/Automata.Benchmarks` de la PR #9 doublonnait
  celui-ci (cf. `BASELINE-net9.0.md` pour la suite `RegexPipelineBenchmarks`).
- Mode multi-runtime (delta .NET 9/10/11, CoursIA #18771) :
  `DOTNET_MULTI_RUNTIME=1 dotnet run -c Release -f net8.0 -- --runtimes net9.0 net10.0 net11.0`
  (job process enfant ; garde `DOTNET_MULTI_RUNTIME` dans les configs InProcess).
- Configuration Release, InProcess (évite le spawn d'un process enfant par
  benchmark, bloqué par Windows Defender sur le poste de mesure).
- **Ne pas ajouter `--job short` sur la ligne de commande** : il AJOUTE un job
  `Default` à côté du job ShortRun InProcess configuré (le présent run l'a fait
  — 6 benchmarks exécutés au lieu de 3 ; les chiffres de référence ci-dessus
  sont ceux du job InProcessEmitToolchain, le job Default ne sert que de
  contre-observation : 8,97 / 39,02 / 27,31 ms, plus rapides car process enfant
  dédié).
- BDN a retiré 1 outlier (21,62 ms) sur IsEquivalent_DFA_Twins_L15.
- Prérequis build : la relativisation des directives `#line` T4 (commit
  `a396028`, PR #9) est nécessaire pour compiler depuis un clone hors
  `C:\GitHub\Automata` — 30 directives sur 3 fichiers Templates.

## Limites connues (honnêteté des mesures)

- Itérations de 21–72 ms (< 100 ms recommandé par BDN) : la structure est
  volontairement figée et de taille modeste (représentative des carnets
  SymbolicAI/SMT) ; grossir le NFA changerait la sémantique de la charge.
- Error ≈ 12–29 % du Mean sur les deux charges GC-bound : poste partagé (GUI
  active), ShortRun borné. Suffisant pour détecter un delta runtime majeur
  (.NET 8 → 11) ; ne pas trancher un delta < 20 % sur ces chiffres seuls.
- Une seule machine ; la comparaison .NET 11 (CoursIA #18771) devra rejouer sur
  le MÊME poste, même toolchain, pour être comparable.
