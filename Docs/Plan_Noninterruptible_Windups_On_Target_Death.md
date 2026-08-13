# Plan: nieprzerywalny windup po śmierci celu

## Cel

Zmienić kontrakt rozpoczętych ataków i speciali tak, aby śmierć zablokowanego
celu nie anulowała rozpoczętej akcji.

- Zwykły atak po śmierci celu kończy windup w pierwotnym terminie.
- W chwili zakończenia windupu zwykły atak może jednokrotnie przełączyć się na
  innego prawidłowego przeciwnika, ale wyłącznie takiego, który już znajduje się
  w zasięgu z bieżącego heksa atakującego.
- Jeżeli nie ma takiego przeciwnika, zwykły atak nadal wykonuje `Fire` w stronę
  pierwotnego celu i rozstrzyga się jako miss: bez obrażeń i efektów trafienia,
  ale z normalnym zużyciem cyklu ataku.
- Special od początku windupu pozostaje związany z pierwotnym celem. Śmierć celu
  nie powoduje retargetowania ani anulowania; special przechodzi przez normalny
  cast, strike'i, pocisk lub efekt końcowy.

Zmiana dotyczy deterministycznej symulacji. Nie zmienia wartości windupu,
cooldownów, zasięgów, obrażeń ani kolejności ticka poza obsługą śmierci celu i
odroczeniem końca bitwy do rozstrzygnięcia zatwierdzonej akcji.

## Kontrakt funkcjonalny

| Akcja | Stan zablokowanego celu | Wynik po zakończeniu windupu |
| --- | --- | --- |
| Zwykły atak | Cel żyje | Atak w pierwotny cel, bez ponownej kontroli zasięgu, jak obecnie |
| Zwykły atak | Cel nie żyje, inny prawidłowy przeciwnik jest już w zasięgu | Jednorazowy deterministyczny retarget i `Fire` w nowy cel |
| Zwykły atak | Cel nie żyje, brak przeciwnika w bieżącym zasięgu | `Fire` w pierwotny cel, wynik miss, bez ruchu i bez nowego windupu |
| Special celowany | Cel nie żyje | Pełne dokończenie speciala na zapisanym celu, bez retargetowania |
| Special obszarowy lub własny | Brak zablokowanego celu | Zachowanie bez zmian |

### Commitment zwykłego ataku

Początek windupu nadal zamraża `LockedAttackTargetUnitId`, sekwencję, czas
zakończenia i snapshot cyklu. Śmierć celu nie zmienia tych pól i nie emituje
`AttackWindupCancelled`.

Retarget jest dozwolony dopiero przy `ElapsedTime >= WindupEndTime`. Nie wolno:

- zmieniać celu wcześniej podczas windupu;
- rozpoczynać ruchu w poszukiwaniu zastępstwa;
- wybierać przeciwnika, do którego trzeba wykonać choć jeden krok;
- ponawiać retargetowania po zebraniu fire intentu w danym ticku.

Jeżeli kilka windupów kończy się równocześnie, obecna granica symultaniczności
pozostaje zachowana. Cele są wybierane podczas kolekcji intentów. Jeżeli wybrany
cel zginie od wcześniejszego intentu rozstrzyganego w tej samej paczce, późniejszy
atak nadal fire'uje w ten cel i staje się missem; nie wykonuje drugiego retargetu.

### Wybór zastępczego celu zwykłego ataku

Dodać w `TargetSelector` bezalokacyjny wybór celu z aktualnego heksa, który:

1. uwzględnia tylko `TargetingRules.CanBeTargeted`;
2. wymaga `Board.Distance(attacker.CurrentHex, candidate.CurrentHex) <= AttackRange`;
3. respektuje obowiązujące reguły targetowania, w tym taunt;
4. zachowuje deterministyczne tie-breaki: mniejszy dystans, niższe HP, niższy
   `UnitId`;
5. wykonuje pojedynczy liniowy przebieg po `simulation.Units`, bez pathfindingu,
   LINQ i nowych kolekcji.

Do sprawdzenia bieżącego zasięgu używać `CurrentHex`, nie planowanego
`MovementDestination`. Dzięki temu helper odpowiada dokładnie na pytanie, czy
atak można wykonać bez ruchu w tym ticku.

Po wyborze zastępstwa `AttackCycleResolver` powinien:

- wykonać `unit.SetTarget(replacement)`;
- wyemitować `UnitTargetChanged` przed `AttackFired`;
- zachować bieżący `AttackSequenceId` i nie emitować nowego
  `AttackWindupStarted`;
- zapisać zastępczy cel w istniejącym fire workspace, aby gameplay i prezentacja
  korzystały z tej samej decyzji;
- oznaczyć nowy cel jako zaangażowany przy normalnym `Fire`.

### Miss zwykłego ataku

Gdy nie istnieje cel zastępczy, do fire workspace trafia pierwotny obiekt celu,
nawet jeśli jest już martwy. Normalna ścieżka `DamageResolver` zwróci wtedy
`DidHit == false` i nie wyemituje `UnitDamaged`, `UnitCrit`, on-hit ani śmierci.

Sam atak ma jednak zostać uznany za wykonany:

- emituje `AttackFired` z pierwotnym `targetId` i ostatnim znanym heksem;
- przechodzi do `Winddown`;
- zużywa `AttackBonusNextCombat`;
- przyznaje atakującemu normalny mana pulse;
- dla ranged tworzy zwykły pocisk do martwego celu, który przy impact emituje
  `ProjectileResolved(didHit: false)`;
- dla melee kończy się bez eventu obrażeń. Nie dodawać osobnego eventu miss, jeśli
  prezentacja nie wykaże takiej konieczności — `AttackFired` plus brak hit/damage
  jest wystarczającym kontraktem.

Brak wpisu celu (uszkodzone lub nieistniejące `LockedAttackTargetUnitId`) może
nadal anulować windup jako błąd stanu. Nowy kontrakt dotyczy śmierci istniejącego
celu, nie nieprawidłowego runtime state.

### Commitment speciala

Po rozpoczęciu windupu speciala jego `LockedSpecialTargetUnitId` pozostaje jedynym
celem aż do zakończenia akcji. Dla wszystkich speciali celowanych stosować lookup
zablokowanego celu bez wymogu `IsAlive`, analogiczny do obecnego
`TryGetLockedTarget` używanego przez Longshot.

Zmiany w `SpecialCycleResolver`:

- w fazie `Windup` nie anulować speciala tylko dlatego, że
  `TryGetLockedLiveTarget` zwraca false;
- w `BeginCast` dla Mega Arrow, Fury Swipes, Longshot i przyszłych speciali
  celowanych wymagać istnienia zablokowanego wroga, ale nie jego żywotności;
- nie wykonywać ponownej selekcji celu w `BeginCast` ani podczas `Casting`;
- zużyć manę, rozpocząć cooldown i recovery tak samo jak przy żywym celu;
- emitować normalne `SpecialCastStarted`, `SpecialStrikeFired` i
  `UnitSpecialActivated`; nie emitować `SpecialWindupCancelled` z powodu śmierci
  celu.

Fury Swipes wymaga dodatkowego ujednolicenia fazy `Casting`: po śmierci celu
wszystkie pozostałe zaplanowane strike'i mają zostać zebrane i odtworzone na tym
samym celu. `DamageResolver` rozstrzygnie je jako miss bez obrażeń. Usunąć
warunek wcześniejszego `CompleteFuryCast` oparty wyłącznie na braku żywego celu;
cast kończy się po zaplanowanej liczbie strike'ów albo przy rzeczywiście
nieprawidłowym/brakującym locku.

To oznacza też, że cel zabity przez pierwszy strike Fury nie przerywa pozostałej
części tego samego speciala. Jest to konieczne, aby „dokończ cały special” było
spójne również wtedy, gdy cel umiera już po zakończeniu windupu.

Nadal anulować aktywną akcję, gdy problem dotyczy wykonawcy, a nie celu:

- śmierć wykonawcy;
- stun, sleep, silence lub inny status blokujący odpowiednią akcję;
- wykryty nielegalny ruch podczas attack windupu;
- brak lub uszkodzony wpis zablokowanego celu.

## Stan obecny i miejsca zmiany

### `AttackCycleResolver`

`CollectCompletedWindups` obecnie wywołuje `TryGetLockedLiveTarget` przed
sprawdzeniem deadline'u i natychmiast anuluje windup po śmierci celu. Należy
rozdzielić walidację atakującego od rozstrzygnięcia celu:

1. śmierć atakującego lub ruch nadal anulują windup;
2. przed deadline'em martwy cel nie powoduje żadnej zmiany;
3. na deadline żywy lock jest używany bez zmian;
4. na deadline martwy lock uruchamia wybór zastępstwa w aktualnym zasięgu;
5. brak zastępstwa dodaje miss intent na pierwotny cel.

Istniejący prealokowany `Workspace` pozostaje granicą symultaniczności. Nie jest
potrzebny nowy stan w `UnitRuntimeState`.

### `SpecialCycleResolver`

Aktualnie ogólna ścieżka windupu i `BeginCast` wymagają żywego celu. Robocza
implementacja Longshot ma już wyjątek pozwalający wystrzelić do martwego locka i
otrzymać projectile miss. Zamiast utrzymywać wyjątek tylko dla Longshot należy
przenieść ten kontrakt do wspólnej obsługi wszystkich speciali celowanych.

Fury Swipes używa żywotności celu także podczas `Casting` oraz przy
`CompleteResolvedFuryCasts`; oba miejsca muszą zostać dostosowane, aby cast nie
kończył się przedwcześnie.

### `BattleTickLoop`

Samo nieanulowanie windupu nie wystarczy. `TryEndBattle` obecnie odkłada koniec
walki dla aktywnych pocisków oraz — w roboczych zmianach Longshot — osobnego
`HasPendingLongshot`. Zastąpić specjalny przypadek ogólnym, bezalokacyjnym
sprawdzeniem zatwierdzonych akcji żywych jednostek:

- `AttackPhase == Windup`;
- `SpecialPhase == Windup` lub `SpecialPhase == Casting`;
- aktywne projectiles pozostają obsługiwane jak obecnie.

Nie czekać na zwykły `Winddown` ani `SpecialPhase.RecoveryLock`, ponieważ właściwa
akcja jest już wtedy zakończona. Dzięki temu śmierć ostatniego przeciwnika nie
zamyka symulacji przed fire/castem, ale walka kończy się natychmiast po
rozstrzygnięciu ostatniej zatwierdzonej akcji lub pocisku.

### Eventy i prezentacja

Nie przewiduje się nowych typów `BattleEvent` ani zmian w `UnitView`.
`BattleUnitPresenter` już ustawia kierunek z heksa zawartego w `AttackFired`,
`SpecialCastStarted` i `SpecialStrikeFired`, więc:

- zwykły retarget może skorygować kierunek dokładnie przy fire;
- miss bez zastępstwa kończy istniejącą animację w stronę ostatniego heksa celu;
- special kontynuuje animację w stronę zapisanego heksa, nawet jeśli widok celu
  został już usunięty po `UnitDied`.

W Play Mode trzeba potwierdzić, że kolejność `UnitDied`, `UnitTargetChanged` i
`AttackFired` nie powoduje powrotu animatora do idle ani błędnego obrotu. Zmiany w
prezentacji dodawać tylko wtedy, gdy test wizualny wykaże problem.

## Plan testów

### `TargetSelectorTests`

Dodać testy bezalokacyjnego wyboru zastępstwa:

1. wybiera tylko przeciwnika już w zasięgu z `CurrentHex`;
2. ignoruje bliższego ścieżkowo przeciwnika wymagającego ruchu;
3. zachowuje deterministyczne tie-breaki dystans, HP, `UnitId`;
4. respektuje taunt i `Untargetable`;
5. nie wybiera jednostki martwej ani sojusznika.

### `AttackCycleResolverTests`

Zastąpić test `DeadLockedTarget_CancelsWindupWithoutFiring` nowym kontraktem i
dodać rozdzielone przypadki:

1. cel umiera przed deadline'em, inny wróg jest w zasięgu — windup trwa do końca,
   sekwencja się nie zmienia, następuje `UnitTargetChanged` i `AttackFired` w nowy
   cel;
2. inny wróg istnieje, ale wymaga ruchu — atak fire'uje w martwy lock i nie zadaje
   obrażeń;
3. brak innego wroga — melee kończy się jako miss bez `AttackWindupCancelled`,
   zużywa bonus następnego ataku, przyznaje manę i przechodzi do `Winddown`;
4. ranged bez zastępstwa tworzy pocisk do martwego celu, a resolver kończy go
   przez `ProjectileResolved(false)`;
5. kilka zastępstw w zasięgu — wybór jest deterministyczny;
6. oryginalny cel nadal żyje, ale wyszedł z zasięgu — zachowane jest obecne
   dokończenie ataku w ten cel;
7. cel wybrany podczas kolekcji ginie od wcześniejszego równoczesnego fire — brak
   drugiego retargetu i wynik miss.

Zachować regresje potwierdzające, że śmierć atakującego, status blokujący oraz
nielegalny ruch nadal anulują windup.

### `SpecialCycleResolverTests`

1. Zastąpić `MegaArrow_TargetDeathDuringWindupCancelsWithoutManaSpend` testem, w
   którym Mega Arrow zużywa manę, rozpoczyna cast, wystrzeliwuje do martwego celu i
   kończy pocisk jako miss.
2. Zachować i uogólnić istniejący test Longshot fire'ującego do martwego locka.
3. Fury Swipes z celem zabitym podczas windupu rozpoczyna cast, emituje pełną
   liczbę strike'ów na ten sam `targetId`, nie zadaje obrażeń i przechodzi przez
   normalne recovery.
4. Cel Fury zabity przez wczesny strike nie przerywa pozostałych strike'ów.
5. Przy drugim żywym przeciwniku targeted special nigdy nie zmienia locka po
   śmierci pierwotnego celu.
6. Brak `SpecialWindupCancelled` po śmierci celu; mana, attack cooldown i recovery
   są rozliczone normalnie.
7. Śmierć wykonawcy oraz stun/sleep/silence nadal anulują lub fizzlują special
   zgodnie z dotychczasowym kontraktem.
8. Slam i Haste Burst zachowują obecne działanie.

### `BattleTickLoopTests`

1. Śmierć ostatniego celu nie kończy bitwy, dopóki zwykły attack windup nie
   wyemituje końcowego `AttackFired`/missa.
2. Aktywny special windup i `Casting` odkładają koniec bitwy do pełnego
   zakończenia speciala.
3. Recovery lock i winddown nie blokują końca bitwy.
4. Aktywny projectile nadal odkłada koniec bitwy do hit/miss resolution.
5. Przy żywym zastępczym celu w zasięgu walka nie jest sztucznie odkładana poza
   normalny przebieg.

## Zakres plików

- `Assets/DeckBattle/Scripts/Battle/TargetSelector.cs`
  — bezalokacyjny wybór prawidłowego celu w aktualnym zasięgu.
- `Assets/DeckBattle/Scripts/Battle/AttackCycleResolver.cs`
  — brak anulowania po śmierci celu, jednorazowy retarget lub miss intent.
- `Assets/DeckBattle/Scripts/Battle/SpecialCycleResolver.cs`
  — wspólny martwy locked target dla targeted speciali i pełny cast Fury.
- `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`
  — ogólne oczekiwanie na zatwierdzone attack/special cycles przed końcem bitwy.
- `Assets/DeckBattle/Tests/EditMode/TargetSelectorTests.cs`
  — reguły wyboru zastępstwa.
- `Assets/DeckBattle/Tests/EditMode/AttackCycleResolverTests.cs`
  — retarget, miss i zachowanie cyklu ataku.
- `Assets/DeckBattle/Tests/EditMode/SpecialCycleResolverTests.cs`
  — brak retargetu/anulowania oraz pełne dokończenie speciali.
- `Assets/DeckBattle/Tests/EditMode/BattleTickLoopTests.cs`
  — odroczenie końca bitwy.
- `Docs/TDD_Attack_Cycle.md`
  — po implementacji zaktualizować sprzeczny kontrakt mówiący, że śmierć celu
  anuluje windup.

Nie przewiduje się zmian w `UnitRuntimeState`, formacie danych jednostek,
prefabach, scenach, shaderach ani ustawieniach URP.

## Kolejność wdrożenia

1. Dodać testy selektora celu w bieżącym zasięgu.
2. Dodać helper selektora i podłączyć go do kolekcji fire intentów.
3. Zmienić testy oraz implementację zwykłego attack windupu.
4. Uogólnić obsługę martwego locked targetu w `SpecialCycleResolver` i usunąć
   wyjątek ograniczony do Longshot.
5. Dokończyć pełny cykl Fury na martwym celu.
6. Zastąpić `HasPendingLongshot` ogólną kontrolą zatwierdzonych akcji.
7. Dodać testy końca bitwy i regresje anulowania przez stan wykonawcy.
8. Zaktualizować aktywną dokumentację TDD.
9. W otwartym Unity Editorze uruchomić przez Unity MCP najpierw wąskie testy Edit
   Mode: `TargetSelectorTests`, `AttackCycleResolverTests`,
   `SpecialCycleResolverTests`, `BattleTickLoopTests`.
10. Po przejściu wąskiego zestawu uruchomić pełny zestaw Edit Mode, a następnie
    wykonać krótki test Play Mode dla melee, ranged, Mega Arrow, Longshot i Fury.

## Wydajność mobilna

- Wybór zastępstwa odbywa się tylko raz, na końcu windupu i tylko po śmierci
  locked targetu.
- Użyć liniowego skanu istniejącej listy jednostek; nie uruchamiać BFS ani pełnego
  pathfindingu dla pytania o bieżący zasięg.
- Nie dodawać LINQ, enumeratorów, list tymczasowych ani alokacji na tick.
- Zachować istniejące prealokowane workspace'y intentów.
- Ogólne sprawdzenie pending actions w `TryEndBattle` ma być pojedynczym liniowym
  przebiegiem bez alokacji.
- Po implementacji sprawdzić w Profilerze `GC Alloc == 0 B` dla
  `AttackCycleResolver.Resolve`, `SpecialCycleResolver.Resolve` i nowego helpera
  targetowania po rozgrzaniu.

Zmiana nie wpływa na URP, overdraw, shader variants, pamięć tekstur ani build
size.

## Kryteria akceptacji

- Śmierć celu nigdy sama nie emituje anulowania trwającego attack lub special
  windupu.
- Zwykły atak wybiera zastępstwo wyłącznie spośród prawidłowych przeciwników już
  w bieżącym zasięgu.
- Zwykły atak bez zastępstwa kończy animację i cykl jako miss, bez obrażeń i
  efektów trafienia.
- Zwykły retarget nie rozpoczyna nowego windupu, nie zmienia sequence ID i nie
  wymaga ruchu.
- Targeted special nigdy nie retargetuje po rozpoczęciu windupu.
- Mega Arrow i Longshot mogą utworzyć pocisk do martwego celu, który kończy się
  jako projectile miss.
- Fury Swipes odtwarza pełną zaplanowaną sekwencję na zablokowanym celu, nawet
  gdy cel nie żyje.
- Mana, bonus następnego ataku, cooldown i recovery są rozliczane tak samo jak
  dla wykonanej akcji przeciw żywemu celowi.
- Bitwa nie kończy się przed rozstrzygnięciem committed windupu, castu lub
  pocisku, ale nie czeka na winddown/recovery.
- Śmierć wykonawcy, blokujące statusy i nielegalny ruch nadal mogą anulować akcję.
- Wynik pozostaje deterministyczny i nie wprowadza alokacji w hot path.

## Ryzyka i decyzje implementacyjne

### Spójność z roboczym Longshot

W bieżącym worktree znajdują się niezacommitowane zmiany Longshot, w tym wyjątek
`TryGetLockedTarget` oraz `HasPendingLongshot`. Implementacja tego planu powinna je
uogólnić, nie nadpisać ani wycofać. Przed edycją trzeba ponownie sprawdzić diff,
ponieważ te same pliki są aktywnie zmodyfikowane.

### Kolejność eventów przy retargecie

`UnitTargetChanged` przed `AttackFired` zapewnia zgodność logicznego celu,
engagementu i kierunku prezentacji. Test powinien utrwalić tę kolejność, aby
późniejszy refactor `RefreshTargets` nie przywrócił chwilowego wskazania martwego
celu.

### Ostatnia akcja bitwy

Odroczenie końca walki powinno obejmować fazę wykonania akcji, a nie jej recovery.
Zbyt szeroki warunek mógłby sztucznie przedłużać wynik bitwy o cały cooldown;
zbyt wąski ponownie ucinałby animation/event sequence po śmierci ostatniego celu.

### Znaczenie missa dla speciala wielouderzeniowego

Strike w martwy cel powinien nadal emitować event prezentacyjny, ale nie event
damage/hit. Nie należy naliczać dodatkowej many ani efektów on-hit za poszczególne
missy. Jeżeli przyszły special ma koszt per skuteczne trafienie, powinien opierać
się na `HitResolutionResult.DidHit`, nie na samym `SpecialStrikeFired`.
