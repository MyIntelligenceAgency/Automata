# Contrôle croisé du 2026-11-10 — une seule compilation, trois runtimes

Document de contrôle du banc `src/Automata.Benchmarks`, conforme au protocole
de référence des sessions du 20/10 et du 29/10 : **une seule compilation**, jouée
sous plusieurs runtimes, de sorte que le delta intra-session mesure le runtime
seul, compilateur constant.

Il est publié le **même jour** que `COMPARISON-net8-net11-2026-11-10.md`, et il
en **retire** le verdict principal.

## Pourquoi ce contrôle

`COMPARISON-net8-net11-2026-11-10.md` compare **quatre compilations**, une par
TFM (`net8.0`, `net9.0`, `net10.0`, `net11.0`). C'est un choix assumé dans ce
document — il correspond au protocole des baselines amont (`dotnet run -c Release`
par TFM) et il évite l'artefact du roll-forward. Mais il a une conséquence
qui n'y était pas mesurée : **comparer des runtimes sur des IL différents
revient à comparer deux programmes, pas deux runtimes**. L'écart observé
mélange alors l'effet runtime et l'effet compilation.

Le contrôle rejoue le même banc dans le protocole de référence, pour
séparer les deux effets et dire ce qui survit.

## Protocole

| Paramètre | Valeur |
| --- | --- |
| Poste | `myia-ai-01` — Windows 11 Pro (10.0.26300.9711), Intel Core i9-14900KF 3,20 GHz, 32 cœurs logiques / 24 physiques |
| Compilation | **une seule**, `net9.0`, Release — SHA-256 `150be1f2546eb8e5be4401d83b9860490ede13280230be256d35b535db8b34ee` ; le **même fichier** est exécuté sous les trois runtimes |
| Runtimes | .NET **9.0.22** · **10.0.14** · **11.0.0** (rc.3.26522.148), sélectionnés par `dotnet exec --fx-version <version exacte>` |
| Preuve du runtime effectif | en-tête `// Runtime=.NET X.Y.Z (build, …)` de chaque journal BDN, relevé sur les 15 journaux : une seule version par runtime, sur les 5 passes |
| Config BDN | 0.15.8, `InProcessEmitToolchain`, ShortRun 5 warmup / 15 itérations / 1 invocation / unroll 1, `ServerGarbageCollection = false` — identique à la session du 11/10 |
| Passes | **5** passes complètes, **ordre entrelacé** (net9 → net10 → net11 dans chaque passe), **médiane des 5 moyennes** par cellule |
| Charge de l'hôte | CPU 52 % à 91 % sur 15 échantillons (un avant chaque exécution), 1079 à 1099 processus resident |
| Commit mesuré | `e6193d9b0988b7cf9d8023bd3a8291baca4b73f4` (master de ce dépôt) — **identique au gitlink épinglé par CoursIA** |

## Mesures

Médiane des 5 moyennes ; intervalle [min, max] des 5 passes ; unités : µs.

| Benchmark | net9.0.22 | net10.0.14 | net11.0 (rc.3) | 9→10 | 10→11 | 9→11 |
| --- | --- | --- | --- | ---: | ---: | ---: |
| `Determinize_NFA_L15` | 12 510 [11 080 ; 17 500] | 14 830 [14 060 ; 17 660] | 11 530 [10 610 ; 14 120] | +18,5 % | −22,3 % | −7,8 % |
| `Determinize_plus_MinimizeHopcroft_L15` | 47 600 [35 810 ; 54 140] | 40 870 [33 960 ; 48 530] | 48 570 [36 760 ; 57 920] | −14,1 % | +18,8 % | +2,0 % |
| `IsEquivalent_DFA_Twins_L15` | 14 440 [13 420 ; 16 390] | 17 010 [14 810 ; 17 970] | 12 930 [12 270 ; 19 630] | +17,8 % | −24,0 % | −10,5 % |
| `Matching_DotNetDelegate` | 419 [281 ; 702] | 603 [438 ; 753] | 542 [349 ; 718] | +43,9 % | −10,2 % | +29,2 % |
| `Pipeline_ConvertDeterminizeMinimize` | 1 467 [1 061 ; 1 787] | 1 261 [870 ; 1 795] | 1 229 [985 ; 1 403] | **−14,0 %** | −2,6 % | −16,2 % |
| `Pipeline_EvilRegex` | 954 [945 ; 1 249] | 842 [735 ; 1 068] | 1 016 [711 ; 1 084] | −11,7 % | +20,7 % | +6,6 % |

Allocations sur `Pipeline_ConvertDeterminizeMinimize` : 2 712,74 KB (net9),
2 706,04 KB (net10), 2 705,13 KB (net11) — écart de 0,25 % et 0,28 %.

## Résultat

**Aucune des 18 paires testées n'est séparable** : tous les intervalles se
recouvrent. Le critère est inchangé — deux runtimes sont séparables sur un axe
si l'intervalle [min, max] des 5 moyennes de l'un et celui de l'autre ne se
recouvrent pas.

Ce qui change par rapport à `COMPARISON-net8-net11-2026-11-10.md` :

| Verdict de la session du 11/10 (compilations par TFM) | Contrôle à compilation unique |
| --- | --- |
| `Pipeline_ConvertDeterminizeMinimize` net9 → net10 : **−26,8 %**, intervalles disjoints, **établi** | **−14,0 %**, intervalles qui se recouvrent → **non établi** |
| `Determinize_plus_MinimizeHopcroft_L15` net8 → net9 : **−20,8 %**, **établi** | **non testable** ici : net8 n'entre pas dans une comparaison à IL constante |

Lecture, sans arrondi favorable :

1. **Le « rattrapage .NET 10 » n'est pas établi sous le protocole de
   référence.** L'amplitude publiée (−26,8 %) était donc, au moins pour partie,
   un effet de compilation.
2. **Le gain .NET 11 reste non établi**, et par une seconde voie : la session
   l'établissait par l'absence de paire séparable en 10→11 ; le contrôle
   retrouve ce verdict à l'identique et referme les axes 9→10 et 8→9 que la
   session laissait ouverts.
3. **L'axe allocation reste le seul axe concluant, et il reste négatif** :
   aucun gain d'allocation attribuable au runtime, .NET 10 comme .NET 11.
4. **net8 n'est pas re-testé** : une comparaison inter-runtime suppose la même
   IL. Le verdict « net9 bat net8 de 20,8 % » reste **non vérifié** — ni
   infirmé, ni confirmé.
5. **Les dispersions du contrôle sont au moins aussi larges** que celles de la
   session (jusqu'à 85,8 % contre 47–69 %) : la charge d'hôte, comparable dans
   les deux cas, n'a pas été plus favorable. Un verdict qui ne survit qu'à un
   tirage favorable n'est pas un verdict établi.
6. **Ce contrôle établit un point négatif, pas une cause** : il dit qu'aucun
   écart inter-runtime n'est établi en compilation constante ; il ne dit pas
   pourquoi la dispersion est si large.

## Ce qui reste vrai de la session du 11/10

- Les **valeurs** de la session sont réelles et reproductibles, avec le
  protocole qu'elle a suivi (une compilation par TFM).
- Le gain .NET 11 y est **non établi** — le contrôle le confirme.
- L'axe allocation y est **concluant et négatif** — le contrôle le confirme.
- La **charge d'hôte** y est mesurée et publiée — le contrôle mesure la sienne.

## Reproduction

```bash
# 1. Une seule compilation (le <TargetFrameworks> du banc n'est pas modifié).
dotnet build src/Automata.Benchmarks -c Release -f net9.0
sha256sum bin/Release/net9.0/Automata.Benchmarks.dll

# 2. Le meme fichier, sous trois runtimes. 5 passes entrelacees :
#    net9 -> net10 -> net11, dans cet ordre, a chaque passe.
dotnet exec --fx-version 9.0.22                bin/Release/net9.0/Automata.Benchmarks.dll --filter '*'
dotnet exec --fx-version 10.0.14               bin/Release/net9.0/Automata.Benchmarks.dll --filter '*'
dotnet exec --fx-version 11.0.0-rc.3.26522.148 bin/Release/net9.0/Automata.Benchmarks.dll --filter '*'
```

Le SHA-256 de la compilation est relevé **avant** la première exécution et
publié ci-dessus : c'est lui qui prouve que les trois runtimes ont joué le
même programme.

## Provenance

EPIC gains runtime — CoursIA issues 18770 et 18695. Contrôle conduit par
`myia-ai-01:CoursIA-2` le 2026-11-10, dans la journée même de la session
publiée par `COMPARISON-net8-net11-2026-11-10.md`. Les 15 rapports BDN du
contrôle sont conservés hors dépôt et reproductibles par les commandes
ci-dessus.
