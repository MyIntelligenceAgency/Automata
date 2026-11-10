# Baseline .NET 11.0 (rc.3) — Automata.Benchmarks

Mesures de référence de l'API publique d'Automata sur **.NET 11 rc.3**, le
runtime que l'EPIC gains runtime cherche à départager (CoursIA issues 18770 et
18695, digest Stephen Toub 2026-09-15). Sœur de `BASELINE-net8.0.md`,
`BASELINE-net9.0.md` et `BASELINE-net10.0.md` (même banc, même protocole BDN) ;
la **table de comparaison des quatre runtimes** (net8.0, net9.0, net10.0,
net11.0), mesurés dans une session unique sur le même poste, est dans
`COMPARISON-net8-net11-2026-11-10.md` — c'est elle qui fait foi pour tout
écart inter-runtime, les valeurs absolues des baselines antérieures ayant été
mesurées sur une autre machine.

## Environnement

| Élément | Valeur |
| --- | --- |
| Date | 2026-11-10 (session 4 runtimes, ~11:55-12:20 locales) |
| OS | Windows 11 Pro (10.0.26300.9711) |
| CPU | Intel Core i9-14900KF 3,20 GHz — 32 cœurs logiques, 24 physiques |
| Runtime | .NET 11.0.0 (11.0.0-rc.3.26522.148, 11.0.26.52348), X64 RyuJIT x86-64-v3 |
| SDK (build) | 11.0.100-rc.3.26522.148 (installé **localement**, sans UAC — cf. reproduction) |
| BenchmarkDotNet | 0.15.8 |
| Toolchain | InProcessEmitToolchain, `InvocationCount=1`, `UnrollFactor=1`, 5 warmup / 15 itérations |
| Passes | 5 passes complètes, **ordre entrelacé** (net8 → net9 → net10 → net11 dans chaque passe), **médiane des 5 moyennes** par cellule |
| Commit mesuré | `e6193d9b0988b7cf9d8023bd3a8291baca4b73f4` (master de ce dépôt) — **identique au gitlink épinglé par CoursIA** sur `MyIA.AI.Notebooks/SymbolicAI/SMT/Automata`, vérifié par `git ls-tree origin/main` |
| Machine | poste de travail partagé (charge d'hôte mesurée — cf. « Limites connues ») |

## Charges mesurées

Les six charges du banc, toutes via l'API publique — aucune I/O, aucun réseau,
structures **figées** (aucun RNG : mêmes opérandes à chaque itération) :

| Benchmark | Charge |
| --- | --- |
| `Determinize_NFA_L15` (baseline) | déterminisation seule d'un epsilon-NFA en 15 couches, 3 branches convergentes vers le pivot suivant (61 états / 90 transitions, 5 classes de caractères BDD rotatives + boucle par branche) |
| `Determinize_plus_MinimizeHopcroft_L15` | même charge + minimisation Hopcroft du DFA obtenu |
| `IsEquivalent_DFA_Twins_L15` | équivalence de 2 DFA de même langage, numérotations d'états inversées (2 déterminisations + exploration croisée) |
| `Pipeline_ConvertDeterminizeMinimize` | pipeline symbolique complet sur `(fox*o\|bar\|ba+zz)` : `CharSetSolver.Convert` → `Determinize` → `Minimize` |
| `Matching_DotNetDelegate` | `new Regex(...).Compile().Matches(sujet)` — compilateur délégué à System.Text.RegularExpressions, sujet de 36 caractères |
| `Pipeline_EvilRegex` | même pipeline sur la regex piégée `^(([a-z])+.)+[A-Z]([a-z])+$` (backtracking exponentiel en SRE, linéaire en DFA) |

Les charges reconstruisent l'automate à chaque invocation : le coût de
construction est identique sur tous les runtimes et fait partie de la charge
commune.

## Résultats (.NET 11.0 rc.3)

Moyennes : **médiane des 5 passes** ; incertitude : médiane de l'`Error` BDN
*intra-passe* (15 itérations) ; allocations : `Allocated` du `MemoryDiagnoser`.

| Benchmark | Mean | Error (intra-passe) | Allocated | Intervalle des 5 passes |
| --- | ---: | ---: | ---: | --- |
| `Determinize_NFA_L15` | 11,810 ms | 2,956 ms (25,0 %) | 11.41 MB | [10,720 .. 12,160] |
| `Determinize_plus_MinimizeHopcroft_L15` | 54,180 ms | 6,734 ms (12,4 %) | 26.06 MB | [39,980 .. 65,880] |
| `IsEquivalent_DFA_Twins_L15` | 15,450 ms | 3,585 ms (23,2 %) | 23.86 MB | [11,930 .. 23,200] |
| `Pipeline_ConvertDeterminizeMinimize` | 1383,0 µs | 298,0 µs (21,5 %) | 2704.8 KB | [1240 .. 1530] µs |
| `Matching_DotNetDelegate` | 562,0 µs | 152,0 µs (27,0 %) | 223.63 KB | [481 .. 732] µs |
| `Pipeline_EvilRegex` | 994,0 µs | 208,0 µs (20,9 %) | 2124.66 KB | [888 .. 1117] µs |

Lecture hot-path (axes Toub visés) :

- **Aucune amélioration d'allocation attribuable au runtime .NET 11** sur ces
  charges : 11,41 MB (contre 11,42), 26,06 MB (contre 26,06), 23,86 MB (contre
  24,08), 2704,8 KB (contre 2709,1), 223,63 KB (contre 220,0), 2124,66 KB
  (contre 2125,7). L'amplitude maximale sur les quatre runtimes est de 0,92 %
  sur les charges en Mo, et de 1,65 % sur la plus petite charge (le matching
  délégué) : **l'axe allocation est le seul de cette session où la mesure est
  concluante, et il conclut à l'absence de gain**.
- **Déterminisation** : 11,41 MB pour un NFA de 61 états — le
  PowerSetStateBuilder alloue ses ensembles-états (HashSet/Dictionary) à chaque
  sous-ensemble exploré. La marge runtime se joue donc sur le **coût de
  l'allocation et des write barriers**, pas sur le volume alloué — nos
  pipelines allouent par design.
- **Minimisation Hopcroft** : la charge la plus GC-bound du banc (Gen0/Gen1/Gen2
  saturés) — 26,06 MB, 2,28× la déterminisation seule.
- **Équivalence** : 24 Mo pour 15,5 ms : l'exploration croisée paie en
  allocations, pas en CPU.
- **Position par rapport à .NET 10** : sur les six axes, les intervalles de
  mesure de .NET 11 et de .NET 10 **se recouvrent** — aucun écart n'est
  séparable du bruit de la session. Le détail et le critère mechanique sont
  dans `COMPARISON-net8-net11-2026-11-10.md` ; aucune conclusion positive ni
  négative sur le fond n'est tirée ici.

## Protocole / reproduction

Le banc ne liste que `net8.0;net9.0` dans ses `<TargetFrameworks>`. Mesurer
.net 11 suppose donc une **édition temporaire d'une ligne**, restaurée ensuite
— le projet est dans `Core.sln`, compilé par la CI en SDK 8.0.x : ajouter
net10/net11 à la liste permanente casserait la porte CI. Une propriété globale
`-p:TargetFrameworks=net11.0` ne convient pas non plus : elle se propage au
projet référencé `Automata.csproj` et provoque `NETSDK1005`.

```bash
# 1. SDK .NET 11 RC3 : ZIP officielle extraite manuellement (Extract-Archive).
#    dotnet-install.ps1 echoue sous Windows -- son extraction "tar -tzf C:\..."
#    lit "C:" comme un hote distant : "Cannot connect to C: resolve failed".
#    Extraction manuelle dans un dossier local, ex. D:/dotnet11 (sans UAC).

# 2. Cibler un runtime : remplacer temporairement <TargetFrameworks> par net11.0,
#    construire avec le SDK 11 local, puis restaurer le fichier.
D:/dotnet11/dotnet.exe build src/Automata.Benchmarks/Automata.Benchmarks.csproj -c Release

# 3. Executer (une passe = un run BDN complet) -- l'hote doit etre le SDK 11 local
cd src/Automata.Benchmarks
D:/dotnet11/dotnet.exe exec bin/Release/net11.0/Automata.Benchmarks.dll --filter '*'
```

Configuration Release, InProcess (évite le spawn d'un process enfant par
benchmark). Sujets et regex constants : résultats reproductibles à la variance
du scheduler près.

## Limites connues (honnêteté des mesures)

- **Charge d'hôte mesurée** : 66 % à 93 % de CPU sur les 20 échantillons prélevés
  pendant les 5 passes (un échantillon isolé à 11 %), 1081 à 1098 processus
  resident, dont 104 `dotnet`/`node`/`Runner`. Le poste héberge en permanence
  le vLLM de la flotte et les runners CI auto-hébergés : **ce n'est pas une
  fenêtre calme**, et les valeurs absolues ci-dessus sont majorées par cette
  charge. Elles ne sont comparables qu'à celles de la même session.
- **Erreur intra-passe de 12 à 27 %** : ShortRun borné (15 itérations) sur un
  poste partagé. L'`Error` BDN ne couvre que la dispersion *intra-passe* ; la
  dispersion *inter-passes* (12 à 64 % sur les cellules de cette baseline) est
  le terme dominant et c'est elle qui sert de critère de séparabilité.
- **Version prerelease** : .NET 11 rc.3 — une rc ultérieure peut se comporter
  différemment ; les gains de cette branche .NET 10 → 11 ne sont, à ce jour,
  **établis sur aucun axe** de ce banc.
- **Une seule machine** : comme pour les baselines net8/net9, la comparaison
  inter-runtime doit se rejouer sur le MÊME poste, même toolchain, même
  session — c'est ce que fait `COMPARISON-net8-net11-2026-11-10.md`.
- Le `Pipeline_Compile` reste absent : `Automaton<S>.Compile()` passe par
  `System.CodeDom.Compiler.CodeDomProvider`, supprimé des runtimes modernes —
  plafond INTRINSIC déjà documenté dans `BASELINE-net9.0.md`.
