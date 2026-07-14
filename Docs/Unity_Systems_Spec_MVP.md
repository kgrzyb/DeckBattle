# Deck Battle - Unity Systems Spec MVP

## 1. Cel Dokumentu

Ten dokument opisuje proponowana strukture systemow Unity dla MVP gry Deck Battle. Specyfikacja jest oparta na `Docs/GDD_MVP.md` i ma sluzyc jako techniczny punkt odniesienia przed implementacja.

Priorytety:

- prosta architektura,
- separacja logiki gry od prezentacji,
- deterministyczna walka,
- niski koszt CPU, GC i renderingu na mobile,
- latwe testowanie logiki w edit mode.

## 2. Docelowa Struktura Folderow

Proponowany uklad w `Assets`:

```text
Assets/
  DeckBattle/
    Art/
      Materials/
      Models/
      VFX/
    Audio/
    Data/
      Units/
      Encounters/
      Boosters/
    Prefabs/
      Battle/
      UI/
      Units/
    Scenes/
      MainMenu.unity
      Battle.unity
    Scripts/
      Battle/
      Board/
      Cards/
      Collection/
      Combat/
      Core/
      Data/
      Input/
      UI/
      Utility/
    Tests/
      EditMode/
      PlayMode/
```

Na start nie trzeba tworzyc wszystkich folderow naraz. Foldery powinny powstawac razem z realnymi plikami.

## 3. Sceny MVP

### MainMenu

Scena startowa MVP.

Odpowiedzialnosci:

- pokazanie prostego menu,
- wejscie do Collection,
- wejscie do Deck Builder,
- start meczu testowego,
- przejscie do Battle.

W MVP Collection i Deck Builder moga byc panelami UI w tej samej scenie.

### Battle

Glowna scena walki.

Odpowiedzialnosci:

- arena hex 3D,
- kamera portretowa,
- jednostki 3D,
- UI walki,
- faza przygotowania,
- auto-battle,
- wynik meczu.

## 4. Glowne Warstwy Systemu

### Data Layer

Dane statyczne gry, glownie `ScriptableObject`.

Przyklady:

- definicje jednostek,
- definicje encounterow AI,
- definicje boosterow,
- ustawienia meczu.

### Runtime Logic Layer

Czysta logika C# bez zaleznosci od Unity scene objects tam, gdzie jest to praktyczne.

Przyklady:

- stan meczu,
- reka gracza,
- deck,
- armia,
- pozycje formacji,
- rozstrzyganie akcji,
- kalkulacja obrazen po rundzie.

### Presentation Layer

MonoBehaviours odpowiedzialne za scene, prefaby, animacje, input i UI.

Przyklady:

- widok hexa,
- widok jednostki,
- karta UI,
- kontroler kamery,
- animacje ruchu i ataku.

## 5. ScriptableObject Data

### UnitDefinition

Definicja jednej unikalnej jednostki.

Proponowane pola:

```csharp
public sealed class UnitDefinition : ScriptableObject
{
    public string UnitId;
    public string DisplayName;
    public UnitType UnitType;
    public UnitRarity Rarity;
    public int ApCost;
    public int MaxHp;
    public int Attack;
    public int Power;
    public int AttackRange;
    public int MoveRange;
    public float AttackCooldown;
    public GameObject UnitPrefab;
    public Sprite CardArt;
}
```

Uwagi:

- `UnitId` musi byc stabilnym identyfikatorem do save data.
- `UnitPrefab` moze na poczatku wskazywac prosty placeholder 3D.
- `CardArt` moze byc puste w pierwszym prototypie.
- Nie przechowywac runtime HP ani pozycji w `UnitDefinition`.

### BattleConfig

Konfiguracja zasad meczu.

Proponowane pola:

```csharp
public sealed class BattleConfig : ScriptableObject
{
    public int StartingPlayerHp;
    public int StartingEnemyHp;
    public int StartingAp;
    public int MaxAp;
    public int StartingHandSize;
    public int DrawPerRound;
    public int StartingDeploymentSlots;
    public int MaxDeploymentSlots;
    public int DeploymentSlotIncreaseEveryRounds;
    public int BoardWidth;
    public int BoardHeight;
}
```

### EnemyEncounterDefinition

Definicja prostego przeciwnika AI.

Proponowane pola:

```csharp
public sealed class EnemyEncounterDefinition : ScriptableObject
{
    public string EncounterId;
    public string DisplayName;
    public List<UnitDefinition> Deck;
}
```

### BoosterDefinition

Definicja boostera.

Proponowane pola:

```csharp
public sealed class BoosterDefinition : ScriptableObject
{
    public string BoosterId;
    public int RewardCount;
    public List<UnitDefinition> PossibleUnits;
}
```

## 6. Runtime Modele

Runtime modele powinny byc zwyklymi klasami C#, bez dziedziczenia po `MonoBehaviour`.

### RuntimeUnit

Stan jednostki w trakcie meczu.

Odpowiedzialnosci:

- trzyma referencje do `UnitDefinition`,
- przechowuje aktualne HP w rundzie,
- przechowuje wlasciciela,
- przechowuje zapisane pole formacji,
- przechowuje aktualne pole bojowe podczas walki,
- wie, czy jednostka jest pokonana w aktualnej rundzie.

Proponowane pola:

```csharp
public sealed class RuntimeUnit
{
    public int RuntimeId;
    public UnitDefinition Definition;
    public BattleSide Side;
    public int CurrentHp;
    public HexCoord FormationCoord;
    public HexCoord BattleCoord;
    public bool IsDefeated;
}
```

### BattleState

Pelny stan meczu.

Odpowiedzialnosci:

- HP gracza i przeciwnika,
- numer rundy,
- AP,
- decki,
- reka,
- zagrane jednostki,
- aktywne sloty,
- aktualna faza meczu.

### PlayerBattleState

Stan jednej strony meczu.

Odpowiedzialnosci:

- HP,
- AP,
- deck,
- hand,
- played units,
- formation units,
- deployment slots.

### CardRuntimeState

Stan karty podczas meczu.

Odpowiedzialnosci:

- wskazuje `UnitDefinition`,
- okresla, czy karta jest w decku, rece, discardzie lub juz zagrana,
- nie przechowuje statystyk bojowych.

## 7. Board System

### HexCoord

Wspolrzedne hexa jako wartosc.

Rekomendacja:

- uzyc axial coordinates: `q`, `r`,
- trzymac jako `struct`,
- nie alokowac list w hot path.

Przyklad:

```csharp
public readonly struct HexCoord
{
    public readonly int Q;
    public readonly int R;
}
```

### HexBoard

Czysta reprezentacja planszy.

Odpowiedzialnosci:

- zna rozmiar planszy,
- sprawdza, czy pole istnieje,
- sprawdza, czy pole nalezy do strony gracza lub AI,
- zwraca sasiadow,
- liczy dystans hexowy,
- konwertuje pozycje logiczne do pozycji lokalnej sceny.

### BoardPresenter

MonoBehaviour w scenie.

Odpowiedzialnosci:

- tworzy lub laczy widoki pol hex,
- podswietla dozwolone pola,
- pokazuje zaznaczenie,
- przekazuje klikniecia/tapy do input systemu.

### HexTileView

Widok pojedynczego pola.

Odpowiedzialnosci:

- material bazowy,
- stan podswietlenia,
- informacja o przypisanym `HexCoord`,
- obsluga prostego tap/click.

## 8. Battle Flow

### BattlePhase

Proponowany enum:

```csharp
public enum BattlePhase
{
    None,
    RoundStart,
    Preparation,
    EnemyPreparation,
    Combat,
    RoundResolution,
    MatchEnd
}
```

### BattleController

Glowny koordynator sceny Battle.

Odpowiedzialnosci:

- inicjalizuje mecz,
- zmienia fazy,
- wywoluje dobieranie kart,
- przyjmuje akcje gracza,
- wywoluje AI przygotowania,
- startuje symulacje walki,
- wywoluje rozstrzygniecie rundy,
- publikuje zmiany do UI.

`BattleController` nie powinien zawierac szczegolowej logiki ruchu, targetowania ani UI.

### Round Flow

Proponowany przeplyw:

```text
RoundStart
  -> restore formation positions
  -> reset round HP for all active units
  -> increase AP and deployment slots
  -> draw cards

Preparation
  -> player plays units from hand
  -> player moves formation units
  -> player confirms Ready

EnemyPreparation
  -> AI plays units
  -> AI sets formation

Combat
  -> units fight automatically
  -> combat ends when one or both sides have no active units

RoundResolution
  -> calculate survivor Power damage
  -> apply player/enemy HP damage
  -> check match end
  -> keep formation assignments

MatchEnd or next RoundStart
```

## 9. Card And Deck Systems

### DeckService

Czysta logika decku.

Odpowiedzialnosci:

- tworzy runtime deck z listy `UnitDefinition`,
- tasuje deterministycznie przez podany RNG,
- dobiera karty,
- pilnuje, ze unikalna jednostka moze byc zagrana tylko raz.

### HandService

Odpowiedzialnosci:

- przechowuje karty na rece,
- usuwa karte po zagraniu,
- sprawdza, czy karta moze zostac zagrana za aktualne AP.

### UnitPlayService

Odpowiedzialnosci:

- waliduje zagranie jednostki,
- sprawdza AP,
- sprawdza sloty,
- sprawdza dozwolone pole,
- tworzy `RuntimeUnit`,
- zapisuje pozycje formacji.

Walidacja powinna zwracac wynik, nie tylko `bool`, zeby UI moglo pokazac powod odmowy.

Przyklad:

```csharp
public enum PlayUnitFailReason
{
    None,
    NotEnoughAp,
    NoDeploymentSlot,
    InvalidTile,
    TileOccupied,
    UnitAlreadyPlayed
}
```

## 10. Formation System

### FormationService

Odpowiedzialnosci:

- trzyma przypisania jednostek do pol formacji,
- waliduje przesuniecie jednostki,
- sprawdza zajetosc pol,
- rozroznia strefe gracza i AI,
- przy starcie rundy ustawia `BattleCoord = FormationCoord`.

Zasady MVP:

- przesuwanie jednostek w fazie przygotowania jest darmowe,
- jednostka musi stac na dozwolonym polu swojej strony,
- jedno pole moze miec maksymalnie jedna jednostke,
- pokonana jednostka wraca na pole formacji w kolejnej rundzie.

## 11. Combat System

### CombatSimulator

System auto-battle.

Odpowiedzialnosci:

- wykonuje walke krokami,
- aktualizuje cooldowny ataku,
- wybiera cele,
- zleca ruch,
- aplikuje obrazenia,
- konczy walke, gdy jedna lub obie strony nie maja zywych jednostek.

Rekomendacja MVP:

- symulacja moze byc tick-based,
- tick np. co 0.2 sekundy logicznej walki,
- prezentacja moze interpolowac ruch miedzy polami,
- logika powinna dzialac bez animacji.

### TargetingService

Odpowiedzialnosci:

- znajduje najblizszego przeciwnika,
- rozstrzyga remisy deterministycznie,
- preferuje cele z nizszym HP tylko jesli dystans jest taki sam.

Kolejnosc tie-breakerow:

1. najmniejszy dystans,
2. najnizsze aktualne HP,
3. najnizszy `RuntimeId`.

### MovementService

Odpowiedzialnosci:

- znajduje nastepny hex w strone celu,
- nie pozwala wejsc na zajete pole,
- nie wykonuje kosztownego pathfindingu, jesli cel jest obok,
- w MVP moze uzywac prostego BFS na malej planszy.

Plansza 5x6 lub 5x7 jest mala, wiec BFS per ruch jest akceptowalny w MVP, ale implementacja nie powinna alokowac nowych kolekcji w kazdym ticku walki. Jesli na poczatku pojawia sie alokacje, trzeba je ograniczyc przed budowaniem dluzszej walki.

### DamageService

Odpowiedzialnosci:

- odejmuje HP,
- oznacza jednostke jako pokonana,
- przygotowuje informacje dla prezentacji.

### RoundDamageResolver

Odpowiedzialnosci:

- zlicza `Power` zywych jednostek gracza,
- zlicza `Power` zywych jednostek AI,
- aplikuje obrazenia do HP stron,
- zwraca wynik rundy.

## 12. AI MVP

### EnemyPreparationAI

Prosta logika wystawiania przeciwnika.

Odpowiedzialnosci:

- dobiera karty tak jak gracz,
- zagrywa najdrozsze mozliwe jednostki albo pierwsze dostepne z reki,
- ustawia melee blizej srodka/frontu,
- ustawia range z tylu,
- nie wykonuje zaawansowanego kontrowania gracza.

MVP powinno miec deterministyczne AI, zeby powtarzanie tego samego meczu dalo taki sam wynik przy tym samym seedzie.

## 13. Collection And Booster Systems

### CollectionState

Stan kolekcji gracza.

Odpowiedzialnosci:

- lista posiadanych `UnitId`,
- liczba shardow,
- przyszlosciowo dane progresji.

### CollectionService

Odpowiedzialnosci:

- sprawdza, czy gracz posiada jednostke,
- dodaje nowa jednostke,
- zamienia duplikat na shardy.

### BoosterService

Odpowiedzialnosci:

- losuje nagrody z `BoosterDefinition`,
- uzywa deterministycznego RNG tam, gdzie to potrzebne do testow,
- zwraca liste wynikow: nowa jednostka albo duplikat zamieniony na shardy.

W MVP booster nie powinien wymagac sklepu, monet ani backendu.

## 14. Save System MVP

### SaveGameData

Minimalne dane zapisu:

```csharp
public sealed class SaveGameData
{
    public List<string> OwnedUnitIds;
    public List<string> SelectedDeckUnitIds;
    public int Shards;
}
```

### SaveService

Odpowiedzialnosci:

- zapis lokalny do JSON,
- odczyt przy starcie gry,
- utworzenie domyslnego save, jesli brak pliku,
- walidacja `UnitId` wzgledem dostepnych definicji.

Na mobile zapis powinien uzywac `Application.persistentDataPath`.

## 15. UI Systems

### BattleUIController

Odpowiedzialnosci:

- HP gracza i AI,
- AP,
- numer rundy,
- liczba slotow,
- przycisk `Ready`,
- komunikaty fazy.

UI powinno aktualizowac sie tylko, gdy dane sie zmieniaja. Nie odswiezac tekstow co klatke.

### HandView

Odpowiedzialnosci:

- pokazuje karty na rece,
- obsluguje tap/drag karty,
- blokuje karty, ktorych nie mozna zagrac,
- korzysta z poolingu elementow kart.

### CardView

Odpowiedzialnosci:

- nazwa jednostki,
- koszt AP,
- typ jednostki,
- podstawowe statystyki,
- sygnalizacja dostepnosci.

### CollectionUIController

Odpowiedzialnosci:

- pokazuje posiadane jednostki,
- pokazuje szczegoly jednostki,
- moze uzywac prostego scrolla z poolowaniem pozniej, jesli lista urosnie.

### DeckBuilderUIController

Odpowiedzialnosci:

- pokazuje kolekcje,
- pozwala wybrac deck,
- pilnuje limitu decku,
- nie pozwala wybrac duplikatu.

## 16. Input

### BattleInputController

Odpowiedzialnosci:

- tap na karte,
- drag karty na hex,
- tap jednostki,
- drag jednostki na inny hex w fazie przygotowania,
- tap pola docelowego,
- anulowanie wyboru.

Na MVP mozna zaczac od tap-select:

1. tap na karte lub jednostke,
2. podswietlenie dozwolonych pol,
3. tap na pole,
4. akcja zostaje wykonana.

Drag and drop moze zostac dodany pozniej, gdy podstawowy przeplyw dziala.

## 17. Presentation And Prefabs

### UnitView

MonoBehaviour widoku jednostki.

Odpowiedzialnosci:

- trzyma `RuntimeId`,
- pokazuje model,
- pokazuje pasek HP,
- wykonuje proste animacje ruchu i ataku,
- zmienia kolor lub material strony.

Nie powinien sam decydowac o targetach, obrazeniach ani zasadach walki.

### UnitViewPool

Pooling widokow jednostek.

Odpowiedzialnosci:

- tworzy ograniczona liczbe obiektow,
- zwraca widoki po walce lub po opuszczeniu sceny,
- ogranicza Instantiate/Destroy w trakcie walki.

### BattlePresenter

Odpowiedzialnosci:

- synchronizuje `BattleState` z widokami,
- tworzy widoki dla nowych jednostek,
- ustawia pozycje startowe rundy,
- odpala animacje na podstawie eventow walki.

## 18. Eventy I Komunikacja

Rekomendacja MVP:

- unikac globalnego event busa,
- uzywac jawnych referencji w scenie,
- logika zwraca wyniki akcji,
- `BattleController` przekazuje dane do UI i presenterow.

Dla walki warto wprowadzic lekkie eventy danych:

```csharp
public readonly struct CombatEvent
{
    public CombatEventType Type;
    public int SourceUnitId;
    public int TargetUnitId;
    public HexCoord From;
    public HexCoord To;
    public int Amount;
}
```

Presenter moze odtwarzac ruchy i ataki na podstawie tych eventow. Sama logika walki nie powinna zalezec od animacji.

## 19. RNG I Determinizm

Wszystkie losowe decyzje powinny isc przez jeden kontrolowany generator.

Dotyczy:

- tasowania decku,
- boosterow,
- ewentualnych remisow, jesli beda losowe w przyszlosci.

W MVP targetowanie i AI powinny rozstrzygac remisy deterministycznie bez losowania.

## 20. Mobile Performance Rules Dla Implementacji

Wazne zasady przy pisaniu skryptow:

- unikac LINQ w logice walki, UI refresh i input hot path,
- unikac alokacji w tickach walki,
- cache'owac referencje do komponentow,
- nie uzywac `FindObjectOfType` w runtime flow,
- unikac wielu `Update`; preferowac kontrolowane ticki i eventy,
- poolowac jednostki, karty UI i efekty,
- UI aktualizowac tylko po zmianie danych,
- rozdzielic statyczne UI od czesto zmienianych elementow, jesli canvas rebuild stanie sie problemem.

## 21. URP I Rendering MVP

Zalecenia:

- proste materialy URP Lit lub Unlit,
- ograniczona liczba swiatel realtime,
- bez ciezkiego post-processingu,
- proste cienie albo blob shadows,
- male modele placeholder,
- proste kolory teamow,
- kontrola przezroczystosci i overdraw przy kartach oraz podswietleniach hexow.

## 22. Testy

### Edit Mode Tests

Najwazniejsze testy logiki:

- `HexBoard` sasiedzi i dystans,
- walidacja zagrania jednostki,
- limit slotow,
- przesuwanie formacji,
- dobieranie kart,
- jednostka moze byc zagrana tylko raz,
- zliczanie obrazen z `Power`,
- booster: nowa jednostka vs duplikat na shardy.

### Play Mode Tests

Uzyc tylko tam, gdzie ryzyko uzasadnia koszt:

- start sceny Battle,
- klikniecie/tap wystawienia jednostki,
- podstawowy przeplyw rundy.

## 23. Kolejnosc Implementacji

Rekomendowana kolejnosc:

1. `UnitDefinition`, enumy i testowe dane jednostek.
2. `HexCoord` i `HexBoard`.
3. `BattleConfig` i prosty `BattleState`.
4. Deck, hand i zagrywanie jednostek.
5. Formation system i walidacja pol.
6. Battle scene z placeholder arena.
7. Unit placeholder prefabs i `UnitView`.
8. Prosty Battle UI.
9. Auto-battle bez animacji.
10. Prezentacja ruchu i ataku.
11. Round resolution i koniec meczu.
12. Proste AI przygotowania.
13. Collection, deck builder i save.
14. Booster result po wygranej.

## 24. Minimalny Vertical Slice

Pierwszy grywalny milestone:

- jedna scena Battle,
- 6 testowych jednostek,
- deck gracza ustawiony w Inspectorze,
- deck AI ustawiony w Inspectorze,
- arena 5x6,
- tap-select do zagrania jednostki,
- `Ready`,
- auto-battle,
- obrazenia po rundzie,
- koniec meczu po spadku HP do 0.

Collection, Deck Builder i Booster moga wejsc po potwierdzeniu, ze walka i formacja dzialaja.

## 25. Decyzje Otwarte

Do rozstrzygniecia pozniej:

- finalny klimat i styl artystyczny,
- rozmiar planszy: 5x6 czy 5x7,
- dokladny model animacji walki,
- czy AI przygotowania ma byc naprzemienne z graczem, czy wykonane po `Ready`,
- czy w MVP ma byc rezerwa jednostek po przekroczeniu slotow, czy blokada zagrania.

Rekomendacja MVP:

- arena 5x6,
- AI wykonuje przygotowanie po `Ready`,
- brak rezerwy: nie mozna zagrac jednostki, jesli limit slotow jest pelny.
