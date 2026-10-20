# Baseline .NET 8.0 — Automata.Benchmarks

Mesures de référence de l'API publique d'Automata sur .NET 8, point de départ
des comparaisons .NET 11 (EPIC gains runtime — CoursIA issues 18770 et 18695,
digest Stephen Toub 2026-09-15). Sœur du baseline MetaGeneticSharp
(.NET 9, 2026-10-02) : même protocole, même toolchain.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-10-19 (run de référence du projet porté) |
| OS | Windows 11 Pro (10.0.26300.9711) |
| CPU | 12th Gen Intel Core i7-12700H |
| Runtime | .NET 8.0.32 (8.0.3226.50405), X64 RyuJIT x86-64-v3 |
| SDK (build) | 10.0.113 |
| BenchmarkDotNet | 0.15.8 |
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
| Determinize_NFA_L15 | 37.58 ms | 5.082 ms | 4.754 ms | 1.02 | - | - | - | 11.42 MB | 1.00 |
| Determinize_plus_MinimizeHopcroft_L15 | 81.17 ms | 5.315 ms | 4.971 ms | 2.19 | 2000.0000 | 1000.0000 | 1000.0000 | 26.05 MB | 2.28 |
| IsEquivalent_DFA_Twins_L15 | 41.29 ms | 3.485 ms | 2.910 ms | 1.12 | 1000.0000 | - | - | 24.08 MB | 2.11 |

Lecture hot-path (axes Toub visés) :

- **Déterminisation** : 11,42 MB pour un NFA de 61 états — le
  PowerSetStateBuilder alloue ses ensembles-états (HashSet/Dictionary) à chaque
  sous-ensemble exploré. Levier principal : allocations/write barriers moins
  chères. Les allocations sont reproductibles au byte entre runs (11,42 MB les
  14 et 19/10) — seuls les temps varient avec la charge du poste.
- **Minimisation Hopcroft** : ajoute ~44 ms et +14,6 MB (2,28× l'alloc de la
  déterminisation seule), GC Gen0/Gen1/Gen2 = 2000/1000/1000 pour 1000
  opérations — la charge la plus GC-bound du banc (raffinement de partition,
  `Dictionary.Remove` / HashSet churn).
- **Équivalence** : ratio 1,12 sur ce run (2 déterminisations + exploration
  croisée) et 24,08 MB (2,11×) : l'exploration paie surtout en allocations.
- **Multimodalité** : `Determinize_NFA_L15` présente plusieurs modes (mValue=3
  sur les deux runs) sous ShortRun ; la comparaison .NET 11 devra rejouer le
  même protocole et ne pas trancher sur un unique Mean.

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
  `Default` à côté du job ShortRun InProcess configuré (constaté au run du
  2026-10-14 — 6 benchmarks exécutés au lieu de 3 ; ses contre-observations,
  plus rapides car process enfant dédié, ne sont pas la référence).
- Outliers : le run du 2026-10-19 a retiré 2 valeurs sur
  IsEquivalent_DFA_Twins_L15 (3 détectées : 34,52 / 48,62 / 53,85 ms).
- Prérequis build : la relativisation des directives `#line` T4 (commit
  `a396028`, PR #9) est nécessaire pour compiler depuis un clone hors
  `C:\GitHub\Automata` — 30 directives sur 3 fichiers Templates.

## Limites connues

- Itérations de 21–72 ms (< 100 ms recommandé par BDN) : la structure est
  volontairement figée et de taille modeste (représentative des carnets
  SymbolicAI/SMT) ; grossir le NFA changerait la sémantique de la charge.
- Error ≈ 6,5–13,5 % du Mean sur ce run (12–29 % au run du 2026-10-14) :
  poste partagé (GUI active), ShortRun borné. Suffisant pour détecter un delta
  runtime majeur (.NET 8 → 11) ; ne pas trancher un delta < 20 % sur ces
  chiffres seuls.
- Une seule machine ; la comparaison .NET 11 (CoursIA #18771) devra rejouer sur
  le MÊME poste, même toolchain, pour être comparable.
