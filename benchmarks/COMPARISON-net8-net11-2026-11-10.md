# Comparaison .NET 8.0 → 9.0 → 10.0 → 11.0 (rc.3) — Automata.Benchmarks

Document de référence du banc `src/Automata.Benchmarks` : les **quatre
runtimes mesurés dans une session unique, sur le même poste, avec le même
outillage**. Il est le complément des baselines par runtime
(`BASELINE-net8.0.md`, `BASELINE-net9.0.md`, `BASELINE-net10.0.md`,
`BASELINE-net11.0.md`), qui restent le point d'entrée par version mais dont
les valeurs absolues ont été mesurées sur d'autres machines : **les colonnes
de ce document, elles, ne se comparent qu'entre elles**.

EPIC gains runtime — CoursIA issues 18770 et 18695, digest Stephen Toub
2026-09-15. Les baselines net10.0 et net11.0 n'existaient pas
auparavant dans ce dépôt ; ce document est le premier à les regrouper.

## Pourquoi une session unique

Les sessions antérieures du banc 3 (02/10 sur `myia-po-2026`, 20/10 sur
`myia-po-2023`, 29/10 sur `myia-ai-01`) ont chacune mesuré **trois**
runtimes dans **une** session, mais **sur des postes différents** et avec des
**versions patch de runtime différentes** (rc.1 puis rc.2, ici rc.3). Elles
donnent donc des sessions autonomes, jamais une prolongation les unes des
autres — règle de lecture du document central CoursIA : *« seuls les deltas
intra-session sont comparables »*.

Ce document applique cette règle à quatre runtimes, en une seule fois :
net8.0 entre dans la comparaison pour la première fois, et les deux classes de
banc du dépôt (`AutomatonOpsBench`, `RegexPipelineBenchmarks`) sont mesurées
sur les quatre runtimes — `AutomatonOpsBench` n'avait jamais été comparé
entre runtimes auparavant.

## Protocole

| Paramètre | Valeur |
| --- | --- |
| Poste | `myia-ai-01` — Windows 11 Pro (10.0.26300.9711), Intel Core i9-14900KF 3,20 GHz, 32 cœurs logiques / 24 physiques |
| Runtimes | .NET **8.0.33** · **9.0.22** · **10.0.14** · **11.0.0** (rc.3.26522.148) |
| Preuve du runtime effectif | en-tête `[Host] : .NET X.Y.Z (...)` de chaque rapport BDN, relevé sur les 20 runs |
| SDK (build) | 10.0.114 pour net8/net9/net10 ; 11.0.100-rc.3.26522.148 (installé localement) pour net11 |
| Config BDN | 0.15.8, `InProcessEmitToolchain`, ShortRun 5 warmup / 15 itérations / 1 invocation / unroll 1, `ServerGarbageCollection = false` |
| Passes | **5** passes complètes, **ordre entrelacé** (net8 → net9 → net10 → net11 dans chaque passe), **médiane des 5 moyennes** par cellule |
| Build | un build Release **par TFM** (le `TargetFrameworks` du banc ne liste que `net8.0;net9.0` : édition temporaire d'une ligne, restaurée après mesure — le projet est dans `Core.sln`, compilé par la CI en SDK 8.0.x) |
| Commit mesuré | `e6193d9b0988b7cf9d8023bd3a8291baca4b73f4` — **identique au gitlink épinglé par CoursIA** (`git ls-tree origin/main MyIA.AI.Notebooks/SymbolicAI/SMT/Automata`) |

**Écart assumé** : les sessions du 20/10 et du 29/10 rejouaient **un seul
build** `net9.0` sur les trois runtimes via `dotnet exec --fx-version` (le
delta intra-session y mesure donc le runtime seul, compilateur constant). Ici,
chaque runtime a **son** build. C'est un choix différent, assumé : il
correspond au protocole des baselines amont (`dotnet run -c Release` par
TFM) et il évite l'artefact du roll-forward, au prix d'introduire une
variante de compilateur par TFM. **Les valeurs de ce document ne sont
comparables ni à celles du 20/10, ni à celles du 29/10** — seulement à
elles-mêmes.

## Résultats — classe `AutomatonOpsBench` (ms)

Moyennes = médiane des 5 passes ; ± = médiane de l'`Error` BDN intra-passe.

| Benchmark | net8.0 | net9.0 | net10.0 | net11.0 (rc.3) | Allocated (net8 / net9 / net10 / net11) |
| --- | ---: | ---: | ---: | ---: | --- |
| `Determinize_NFA_L15` | 12,770 ms ± 0,851 | 13,620 ms ± 2,290 | 14,260 ms ± 2,164 | 11,810 ms ± 2,956 | 11.42 MB / 11.42 MB / 11.42 MB / 11.41 MB |
| `Determinize_plus_MinimizeHopcroft_L15` | 50,850 ms ± 7,671 | 40,290 ms ± 7,533 | 44,330 ms ± 9,724 | 54,180 ms ± 6,734 | 26.05 MB / 26.06 MB / 26.06 MB / 26.06 MB |
| `IsEquivalent_DFA_Twins_L15` | 15,580 ms ± 2,867 | 13,240 ms ± 1,378 | 13,860 ms ± 2,235 | 15,450 ms ± 3,585 | 24.08 MB / 24.08 MB / 24.07 MB / 23.86 MB |

## Résultats — classe `RegexPipelineBenchmarks` (µs)

| Benchmark | net8.0 | net9.0 | net10.0 | net11.0 (rc.3) | Allocated (net8 / net9 / net10 / net11) |
| --- | ---: | ---: | ---: | ---: | --- |
| `Matching_DotNetDelegate` | 616,0 µs ± 142,0 | 536,0 µs ± 142,0 | 705,0 µs ± 132,0 | 562,0 µs ± 152,0 | 219.99 KB / 222.77 KB / 223.48 KB / 223.63 KB |
| `Pipeline_ConvertDeterminizeMinimize` | 1792,0 µs ± 400,0 | 1730,0 µs ± 346,0 | 1267,0 µs ± 255,0 | 1383,0 µs ± 298,0 | 2709.13 KB / 2712.74 KB / 2705.76 KB / 2704.8 KB |
| `Pipeline_EvilRegex` | 1328,0 µs ± 317,0 | 1145,0 µs ± 244,0 | 959,0 µs ± 219,0 | 994,0 µs ± 208,0 | 2125.73 KB / 2129.95 KB / 2124.28 KB / 2124.66 KB |

## Les 5 passes, cellule par cellule

C'est la donnée qui permet de juger de la séparabilité : un écart n'est retenu
que si les **intervalles de mesure** (min..max des 5 moyennes de passes) des
deux runtimes comparés **ne se recouvrent pas**.

### `AutomatonOpsBench` (ms)

| Benchmark | Runtime | passe 1 | passe 2 | passe 3 | passe 4 | passe 5 | médiane | intervalle |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `Determinize_NFA_L15` | net8.0 | 15,030 | 12,770 | 12,010 | 12,550 | 15,820 | 12,770 | [12,010 .. 15,820] |
| `Determinize_NFA_L15` | net9.0 | 12,410 | 13,620 | 12,190 | 15,740 | 15,050 | 13,620 | [12,190 .. 15,740] |
| `Determinize_NFA_L15` | net10.0 | 11,680 | 14,260 | 13,020 | 14,880 | 16,410 | 14,260 | [11,680 .. 16,410] |
| `Determinize_NFA_L15` | net11.0 | 12,160 | 10,720 | 11,810 | 11,840 | 10,780 | 11,810 | [10,720 .. 12,160] |
| `Determinize_plus_MinimizeHopcroft_L15` | net8.0 | 57,040 | 50,850 | 46,430 | 55,720 | 47,430 | 50,850 | [46,430 .. 57,040] |
| `Determinize_plus_MinimizeHopcroft_L15` | net9.0 | 42,080 | 38,850 | 39,870 | 41,210 | 40,290 | 40,290 | [38,850 .. 42,080] |
| `Determinize_plus_MinimizeHopcroft_L15` | net10.0 | 43,790 | 51,610 | 44,330 | 43,650 | 49,640 | 44,330 | [43,650 .. 51,610] |
| `Determinize_plus_MinimizeHopcroft_L15` | net11.0 | 47,540 | 39,980 | 55,900 | 65,880 | 54,180 | 54,180 | [39,980 .. 65,880] |
| `IsEquivalent_DFA_Twins_L15` | net8.0 | 20,730 | 16,330 | 13,150 | 15,580 | 14,310 | 15,580 | [13,150 .. 20,730] |
| `IsEquivalent_DFA_Twins_L15` | net9.0 | 12,510 | 12,380 | 13,240 | 16,120 | 13,370 | 13,240 | [12,380 .. 16,120] |
| `IsEquivalent_DFA_Twins_L15` | net10.0 | 18,590 | 13,190 | 13,860 | 12,760 | 15,490 | 13,860 | [12,760 .. 18,590] |
| `IsEquivalent_DFA_Twins_L15` | net11.0 | 12,510 | 15,450 | 11,930 | 17,190 | 23,200 | 15,450 | [11,930 .. 23,200] |

### `RegexPipelineBenchmarks` (ms)

| Benchmark | Runtime | passe 1 | passe 2 | passe 3 | passe 4 | passe 5 | médiane | intervalle |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| `Matching_DotNetDelegate` | net8.0 | 0,608 | 0,616 | 0,723 | 0,603 | 0,650 | 0,616 | [0,603 .. 0,723] |
| `Matching_DotNetDelegate` | net9.0 | 0,480 | 0,677 | 0,439 | 0,536 | 0,670 | 0,536 | [0,439 .. 0,677] |
| `Matching_DotNetDelegate` | net10.0 | 0,705 | 0,598 | 0,795 | 0,521 | 0,762 | 0,705 | [0,521 .. 0,795] |
| `Matching_DotNetDelegate` | net11.0 | 0,562 | 0,732 | 0,553 | 0,682 | 0,481 | 0,562 | [0,481 .. 0,732] |
| `Pipeline_ConvertDeterminizeMinimize` | net8.0 | 1,688 | 1,792 | 2,010 | 1,629 | 2,245 | 1,792 | [1,629 .. 2,245] |
| `Pipeline_ConvertDeterminizeMinimize` | net9.0 | 1,864 | 1,730 | 1,860 | 1,648 | 1,645 | 1,730 | [1,645 .. 1,864] |
| `Pipeline_ConvertDeterminizeMinimize` | net10.0 | 1,509 | 1,267 | 1,466 | 0,943 | 0,876 | 1,267 | [0,876 .. 1,509] |
| `Pipeline_ConvertDeterminizeMinimize` | net11.0 | 1,501 | 1,241 | 1,383 | 1,530 | 1,240 | 1,383 | [1,240 .. 1,530] |
| `Pipeline_EvilRegex` | net8.0 | 1,172 | 1,147 | 1,328 | 1,430 | 1,521 | 1,328 | [1,147 .. 1,521] |
| `Pipeline_EvilRegex` | net9.0 | 1,065 | 1,145 | 1,048 | 1,183 | 1,316 | 1,145 | [1,048 .. 1,316] |
| `Pipeline_EvilRegex` | net10.0 | 1,042 | 0,959 | 1,144 | 0,942 | 0,871 | 0,959 | [0,871 .. 1,144] |
| `Pipeline_EvilRegex` | net11.0 | 1,050 | 0,994 | 0,888 | 0,916 | 1,117 | 0,994 | [0,888 .. 1,117] |

## Écarts inter-runtimes et dispersion

| Benchmark | 8→9 | 9→10 | 10→11 | 9→11 | 8→11 | dispersion inter-passes (max) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Determinize_NFA_L15` | +6,7 % | +4,7 % | -17,2 % | -13,3 % | -7,5 % | 33,7 % |
| `Determinize_plus_MinimizeHopcroft_L15` | -20,8 % | +10,0 % | +22,2 % | +34,5 % | +6,5 % | 48,9 % |
| `IsEquivalent_DFA_Twins_L15` | -15,0 % | +4,7 % | +11,5 % | +16,7 % | -0,8 % | 64,2 % |

| Benchmark | 8→9 | 9→10 | 10→11 | 9→11 | 8→11 | dispersion inter-passes (max) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `Matching_DotNetDelegate` | -13,0 % | +31,5 % | -20,4 % | +4,7 % | -8,9 % | 42,6 % |
| `Pipeline_ConvertDeterminizeMinimize` | -3,4 % | -26,8 % | +9,1 % | -20,1 % | -22,8 % | 53,0 % |
| `Pipeline_EvilRegex` | -13,8 % | -16,2 % | +3,7 % | -13,2 % | -25,2 % | 28,0 % |

## Séparabilité — le critère, et son résultat

Critère : deux runtimes sont **séparables** sur un axe si l'intervalle
[min, max] des 5 moyennes de passes de l'un et celui de l'autre **ne se
recouvrent pas**. 18 paires ont été testées (6 benchmarks × 3 paires de
runtimes consécutifs) ; **3 sont séparables** :

| Benchmark | Paire | Écart | Intervalles |
| --- | --- | ---: | --- |
| `Determinize_plus_MinimizeHopcroft_L15` | net8.0 → net9.0 | **−20,8 %** | [46,430..57,040] vs [38,850..42,080] |
| `Determinize_plus_MinimizeHopcroft_L15` | net9.0 → net10.0 | +10,0 % | [38,850..42,080] vs [43,650..51,610] |
| `Pipeline_ConvertDeterminizeMinimize` | net9.0 → net10.0 | **−26,8 %** | [1,645..1,864] vs [876..1,509] |

Lecture, sans arrondi favorable :

1. **.NET 10 est plus rapide que .NET 9 sur le pipeline symbolique
   allocation-lourd** (−26,8 %, intervalles disjoints) : c'est le seul écart
   franchement établi de la session, et il va **dans le même sens** que le
   −10,9 % de la session du 02/10 sur le même benchmark. Le « rattrapage .NET
   10 » est donc **confirmé sur cet axe**, et son amplitude est plus grande
   que celle du 02/10.
2. **.NET 9 est plus rapide que .NET 8 sur la minimisation Hopcroft**
   (−20,8 %, intervalles disjoints) : donnée **nouvelle** — le banc 3 n'avait
   jamais comparé net8 à net9.
3. **La paire net9 → net10 sur la minimisation Hopcroft (+10,0 %) est à la
   limite exacte du seuil de ±10 %** retenu par le document central CoursIA
   pour ces bancs : elle est listée ici pour être complète, mais **n'est pas
   comptée comme établie**.
4. **Sur la branche 10 → 11, aucune des six paires n'est séparable** : les
   intervalles de .NET 11 et de .NET 10 se recouvrent partout, y compris sur
   le pipeline symbolique où le gain .NET 10 avait été établi. **Aucun gain
   .NET 11 n'est établi sur ce banc**, et aucun écart négatif net n'est établi
   non plus. C'est le résultat que l'EPIC cherche à départager : sur l'API
   publique d'Automata, **le saut 9 → 10 est le seul levier**.
5. **Les allocations ne bougent pas** : amplitude maximale de 0,92 % sur les
   charges en Mo, 1,65 % sur la plus petite charge (le matching délégué). C'est
   le seul axe où la mesure est **concluante** — et elle conclut à l'absence
   de gain runtime sur le volume alloué, ce qui est cohérent avec la nature
   des charges (allouées par design, pas par défaut runtime).

## Charge de l'hôte pendant la fenêtre

| Grandeur | Mesure |
| --- | --- |
| CPU (20 échantillons, un avant chaque exécution) | 66 % à 93 % (un échantillon isolé à 11 %) |
| Processus resident (tous) | 1081 à 1098 |
| Processus `dotnet`/`node`/`Runner` | 104 |

Le poste `myia-ai-01` héberge en permanence le vLLM de la flotte et les
runners CI auto-hébergés. **Ce n'est pas une fenêtre calme** — fait mesuré,
pas une hypothèse : la session du 29/10 sur ce même poste l'avait établi
(62-91 %, 149-150 processus filtrés). Les valeurs absolues ci-dessus sont
majorées par cette charge ; la **comparaison intra-session** l'est moins,
puisque tous les runtimes ont subi la même charge — ce qui reste la limite
assumée d'une session unique.

## Reproduction

```bash
# 1. SDK .NET 11 rc.3 : ZIP officielle extraite manuellement (Extract-Archive).
#    dotnet-install.ps1 echoue sous Windows : son extraction "tar -tzf C:\..."
#    lit "C:" comme un hote distant ("Cannot connect to C: resolve failed").
#    Extraction manuelle dans un dossier local, ex. D:/dotnet11 (sans UAC).

# 2. Un build Release par TFM : le banc ne liste que net8.0;net9.0 dans
#    <TargetFrameworks>. Editer temporairement la valeur, construire, restaurer.
#    (Une propriete globale -p:TargetFrameworks se propage au projet reference
#    Automata.csproj et provoque NETSDK1005.)
dotnet build src/Automata.Benchmarks/Automata.Benchmarks.csproj -c Release

# 3. 5 passes entrelacees : net8 -> net9 -> net10 -> net11, dans cet ordre
#    a chaque passe ; la mediane des 5 moyennes est la valeur publiee.
cd src/Automata.Benchmarks
dotnet exec bin/Release/net8.0/Automata.Benchmarks.dll --filter '*'
dotnet exec bin/Release/net9.0/Automata.Benchmarks.dll --filter '*'
dotnet exec bin/Release/net10.0/Automata.Benchmarks.dll --filter '*'
D:/dotnet11/dotnet.exe exec bin/Release/net11.0/Automata.Benchmarks.dll --filter '*'
```

## Limites connues

- **5 passes** : l'intervalle de mesure est celui de 5 points ; une session plus
  longue resserrerait les intervalles et pourrait rendre séparables des paires
  aujourd'hui dans le bruit. Le sens des conclusions (10 → 11 non séparé sur
  les six axes ; 9 → 10 établi sur le pipeline symbolique) est robuste à ce
  degré de précision, mais **une amplification d'axe par plus de passes reste
  possible**.
- **Poste chargé** : voir la section précédente — la limite assumée d'une
  session unique sur un poste partagé.
- **Versions patch** : 8.0.33 / 9.0.22 / 10.0.14 / 11.0.0-rc.3. Une autre
  session, avec d'autres versions patch, donnerait d'autres valeurs — d'où la
  règle : **on compare une session à elle-même**.
- **Aucune conclusion sur le fond du runtime .NET 11** : ce document établit
  qu'**aucun écart .NET 11 vs .NET 10 n'est séparable du bruit sur cet
  banc**, pas que .NET 11 serait plus lent.
- Le `Pipeline_Compile` reste absent : `Automaton<S>.Compile()` passe par
  `System.CodeDom.Compiler.CodeDomProvider`, supprimé des runtimes modernes —
  plafond INTRINSIC déjà documenté dans `BASELINE-net9.0.md`.
