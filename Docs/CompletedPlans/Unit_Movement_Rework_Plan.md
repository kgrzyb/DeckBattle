# Plan reworku poruszania się jednostek

## 1. Cel dokumentu

Dokument opisuje docelowy rework poruszania się jednostek podczas auto-battle na planszy heksagonalnej. Plan obejmuje logikę wyboru osiągalnego celu, wyszukiwanie najkrótszej ścieżki, zajętość pól, konflikty między jednostkami, integrację z tickiem walki oraz testy.

Rework powinien zachować rozdzielenie symulacji od prezentacji. Symulacja podejmuje decyzje o ruchu, natomiast `BattleView` i `UnitView` jedynie odtwarzają wygenerowane zdarzenia.

## 2. Uzgodnione reguły

### 2.1. Cel ruchu

- Jednostka porusza się w stronę wybranego przeciwnika.
- Celem nawigacji nie jest hex zajmowany przez przeciwnika.
- Celem nawigacji jest najbliższy wolny hex, z którego jednostka może zaatakować przeciwnika.
- Jednostka melee dociera do wolnego pola sąsiadującego z celem.
- Jednostka ranged dociera do wolnego pola znajdującego się w jej zasięgu ataku.
- Jeśli jednostka już znajduje się w zasięgu, nie rozpoczyna ruchu.

### 2.2. Osiągalność celu

- Aktualny cel zostaje zachowany, dopóki istnieje ścieżka do przynajmniej jednej wolnej pozycji ataku.
- Jeśli aktualny cel jest nieosiągalny, jednostka wybiera innego osiągalnego przeciwnika.
- Jeśli żaden żywy przeciwnik nie jest osiągalny, jednostka pozostaje w miejscu.
- Brak możliwego ruchu nie może generować `UnitMoved` ani pozostawiać częściowo zarezerwowanego pola.

Priorytet wyboru spośród osiągalnych przeciwników pozostaje zgodny z obecnymi regułami:

1. najmniejszy dystans hexowy,
2. najniższe aktualne HP,
3. najniższy `UnitId`.

### 2.3. Najkrótsza ścieżka

- Koszt każdego kroku między sąsiednimi hexami wynosi `1`.
- Najkrótsza ścieżka oznacza najmniejszą możliwą liczbę kroków hexowych.
- Przy kilku równie krótkich ścieżkach wybór musi być deterministyczny.
- Remis między równymi pozycjami ataku jest rozstrzygany stabilnym porządkiem współrzędnych.
- Jednostka planuje i rozpoczyna maksymalnie jeden krok logiczny naraz.
- Pełna trasa nie jest zapisywana w stanie jednostki między krokami.
- Po ukończeniu każdego kroku cel, zajętość planszy i najkrótsza ścieżka są sprawdzane ponownie.
- Jednostka będąca w trakcie ruchu nie otrzymuje nowego planu przed dotarciem do `MovementDestination`.

### 2.4. Zajęte pola

Jednostki nie mogą przechodzić przez zajęte hexy.

Podczas wyszukiwania ścieżki niedostępne są:

- hexy poza planszą,
- statycznie nieprzechodnie hexy,
- aktualne pozycje wszystkich pozostałych żywych jednostek,
- miejsca docelowe trwających ruchów,
- pola zarezerwowane przez wcześniej zaakceptowane intencje w bieżącym cyklu planowania.

Aktualny hex planującej jednostki jest dopuszczalnym początkiem ścieżki, ale nie może zostać wybrany jako nowy krok ruchu.

Jednostka w trakcie ruchu blokuje zarówno swój aktualny hex, jak i `MovementDestination`. Zapobiega to wejściu innej jednostki na pole, zanim pierwszy ruch zostanie logicznie zatwierdzony.

### 2.5. Konflikty o następny hex

Intencje ruchu są rozpatrywane deterministycznie według:

1. krótszej pozostałej ścieżki do pozycji ataku,
2. niższego `UnitId`.

Jeżeli dwie jednostki chcą wejść na ten sam hex i są wzajemnie swoimi celami:

- jednostka z wyższym priorytetem rezerwuje pole,
- przegrana jednostka pozostaje w miejscu,
- przegrana jednostka nie szuka alternatywnego kroku w tym cyklu.

Jeżeli jednostki mają różne cele:

- jednostka z wyższym priorytetem rezerwuje pole,
- przegrana jednostka ponownie szuka ścieżki do swojego dotychczasowego celu,
- zajęte i już zarezerwowane pola są traktowane jako przeszkody,
- jeśli istnieje nowa ścieżka, jednostka wykonuje jej pierwszy krok,
- jeśli nie istnieje, jednostka pozostaje w miejscu i ponowi planowanie w kolejnym cyklu.

Alternatywna ścieżka może być dłuższa od pierwotnej, ale musi być najkrótszą ścieżką dostępną po uwzględnieniu dokonanych rezerwacji. Jednostka nie zmienia celu w trakcie rozwiązywania konfliktu. Ewentualny nowy cel zostanie wybrany podczas następnego pełnego planowania.

Losowe rozstrzyganie konfliktów nie jest dozwolone.

## 3. Docelowy przepływ ticka

Logika ticka walki powinna wykonywać operacje w następującej kolejności:

1. Zmniejszenie pozostałego czasu rozpoczętych ruchów.
2. Zatwierdzenie jednostek, które dotarły do `MovementDestination`.
3. Aktualizacja logicznych pozycji i mapy zajętości.
4. Obsługa pocisków, obrażeń i śmierci.
5. Odświeżenie celów żywych jednostek.
6. Rozstrzygnięcie ataków.
7. Ponowna walidacja jednostek, celów i zajętości po walce.
8. Zbudowanie intencji następnego kroku.
9. Rozwiązanie konfliktów i rezerwacja pól.
10. Rozpoczęcie zaakceptowanych ruchów i wygenerowanie `UnitMoved`.

Prezentacja interpoluje ruch wyłącznie między dwoma sąsiednimi hexami wskazanymi przez zdarzenie. Animacja nie zmienia ani nie zatwierdza stanu symulacji.

## 4. Plan zmian w kodzie

### 4.1. `HexBoard`

Plik: `Assets/DeckBattle/Scripts/Board/HexBoard.cs`

Należy dodać bezalokacyjne zapytanie wyszukujące najkrótszą ścieżkę do jednego z wielu pól docelowych. Roboczy kontrakt może odpowiadać metodzie `TryFindShortestPathToAny` przyjmującej:

- hex startowy,
- listę dopuszczalnych pozycji ataku,
- dynamicznie zablokowane pola,
- wielokrotnie używany workspace.

Wynik powinien zawierać:

- wybraną pozycję ataku,
- następny hex,
- liczbę kroków do pozycji ataku.

BFS powinien przetworzyć cały poziom, na którym znaleziono pierwszy cel. Pozwoli to wybrać deterministycznie najlepszy hex spośród kilku celów znajdujących się w tej samej minimalnej odległości.

Istniejące API `TryFindPath` może pozostać dla dotychczasowych wywołań i testów.

### 4.2. `AttackPositionSelector`

Plik: `Assets/DeckBattle/Scripts/Battle/AttackPositionSelector.cs`

Selektor powinien budować listę wolnych pozycji ataku i wykonywać jedno wielocelowe wyszukiwanie BFS zamiast osobnego wyszukiwania dla każdego kandydata.

Nowy wynik, roboczo `AttackPathResult`, powinien zawierać:

- `AttackPosition`,
- `NextStep`,
- `PathSteps`,
- informację, czy jednostka już znajduje się w zasięgu.

Wynik powinien być strukturą wartości i nie może przechowywać kolekcji tworzonych dla pojedynczego zapytania.

### 4.3. `TargetSelector`

Plik: `Assets/DeckBattle/Scripts/Battle/TargetSelector.cs`

Targetowanie powinno zwracać pełny wynik wyboru, roboczo `TargetSelection`, zawierający:

- wybranego przeciwnika,
- wynik wyszukiwania pozycji ataku,
- pierwszy krok i długość ścieżki.

Pozwoli to uniknąć ponownego wyszukiwania tej samej ścieżki przez `MovementResolver`.

Algorytm:

1. Sprawdzić aktualny cel.
2. Jeśli cel żyje, jest przeciwnikiem i ma osiągalną pozycję ataku, zachować go.
3. W przeciwnym razie sprawdzać pozostałych przeciwników według istniejącego priorytetu.
4. Wybrać pierwszego przeciwnika z osiągalną pozycją ataku.
5. Jeśli nie istnieje taki przeciwnik, zwrócić brak wyboru.

### 4.4. `MovementResolver`

Plik: `Assets/DeckBattle/Scripts/Battle/MovementResolver.cs`

Resolver powinien zostać podzielony na wyraźne fazy:

1. zebranie aktualnej zajętości,
2. zbudowanie intencji ruchu,
3. deterministyczne uporządkowanie intencji,
4. rozwiązanie konfliktów,
5. rezerwacja zaakceptowanych pól,
6. rozpoczęcie ruchów.

Należy:

- wykorzystać pełny wynik `TargetSelection`,
- usunąć ponowne wyszukiwanie podstawowej ścieżki,
- usunąć losowanie zwycięzcy konfliktu wzajemnych celów,
- zastąpić wybór dowolnego sąsiedniego pola pełnym ponownym BFS dla tego samego celu,
- nie zapisywać rezerwacji dla jednostek, które ostatecznie pozostają w miejscu,
- zachować walidację bezpośrednio przed `StartUnitMovement`.

### 4.5. `BattleTickLoop`

Plik: `Assets/DeckBattle/Scripts/Battle/BattleTickLoop.cs`

Ukończenie trwających kroków powinno nastąpić przed odświeżeniem celów i rozstrzygnięciem walki. Dzięki temu zasięg, targetowanie i kolejny ruch korzystają z tej samej aktualnej pozycji logicznej.

Po obrażeniach i śmierci zajętość musi zostać ponownie uwzględniona przed planowaniem ruchu.

### 4.6. `BattleSimulation`

Plik: `Assets/DeckBattle/Scripts/Battle/BattleSimulation.cs`

Metody `StartUnitMovement` i `CompleteUnitMovement` pozostają ostatnią warstwą walidacji. Powinny nadal odrzucać:

- nieprzechodnie miejsce docelowe,
- niesąsiedni krok,
- miejsce zajęte przez inną jednostkę,
- próbę rozpoczęcia kolejnego ruchu przed ukończeniem poprzedniego.

## 5. Workspace i wydajność mobilna

Wszystkie dane robocze muszą być utworzone przed rozpoczęciem gorącej pętli walki i wykorzystywane ponownie.

Workspace powinien przechowywać:

- kolejkę BFS,
- odwiedzone pola,
- poprzedników ścieżki,
- listę pozycji ataku,
- mapę zajętości,
- mapę rezerwacji,
- listę intencji,
- bufory używane przy ponownym planowaniu konfliktów.

W ticku walki należy unikać:

- LINQ,
- nowych list, słowników i zbiorów,
- delegatów tworzonych dla zapytań pathfindingu,
- boxingu,
- wielokrotnego pobierania komponentów Unity.

Plansza 5x6 lub 5x7 jest wystarczająco mała, aby BFS po każdym ukończonym kroku był akceptowalny. Priorytetem jest stabilny koszt ticka i brak `GC Alloc`, a nie wczesne cache'owanie pełnych tras.

## 6. Plan testów

### 6.1. `HexBoardTests`

Plik: `Assets/DeckBattle/Tests/EditMode/HexBoardTests.cs`

Dodać testy:

- najkrótszej ścieżki do jednego z wielu celów,
- deterministycznego wyboru przy równych ścieżkach,
- braku przejścia przez zajęty hex,
- najkrótszego objazdu przeszkody,
- braku wyniku przy całkowitym odcięciu,
- dopuszczenia własnego hexa wyłącznie jako początku ścieżki,
- poprawnego ponownego wykorzystania workspace.

### 6.2. `AttackPositionSelectorTests`

Plik: `Assets/DeckBattle/Tests/EditMode/AttackPositionSelectorTests.cs`

Dodać lub dostosować testy:

- melee wybiera najbliższego wolnego sąsiada celu,
- ranged wybiera najbliższy osiągalny hex w zasięgu,
- zajęta pozycja ataku jest pomijana,
- krótsza ścieżka wygrywa z mniejszym dystansem geometrycznym,
- brak wolnej i osiągalnej pozycji zwraca `false`,
- jednostka będąca w zasięgu pozostaje na aktualnym hexie.

### 6.3. `TargetSelectorTests`

Plik: `Assets/DeckBattle/Tests/EditMode/TargetSelectorTests.cs`

Dodać lub dostosować testy:

- aktualny osiągalny cel zostaje zachowany,
- aktualny nieosiągalny cel zostaje zastąpiony innym osiągalnym,
- nieosiągalny najbliższy przeciwnik jest pomijany,
- brak osiągalnych przeciwników zwraca brak celu,
- remisy pozostają deterministyczne.

### 6.4. `MovementResolverTests`

Plik: `Assets/DeckBattle/Tests/EditMode/MovementResolverTests.cs`

Dodać lub dostosować testy:

- każdy wykonany krok należy do aktualnie najkrótszej ścieżki,
- trasa jest przeliczana po ukończeniu każdego hexa,
- pojawienie się przeszkody powoduje wybór nowej najkrótszej trasy,
- jednostka nie wchodzi ani nie przechodzi przez zajęty hex,
- całkowicie odcięta jednostka nie porusza się,
- ruch blokuje aktualny hex i `MovementDestination`,
- dwie jednostki nigdy nie rezerwują ani nie zajmują tego samego pola,
- przy wzajemnych celach zwycięzca rusza, a przegrany pozostaje,
- przy różnych celach przegrany ponownie wyszukuje trasę,
- brak alternatywnej trasy powoduje pozostanie w miejscu,
- wynik konfliktów nie zależy od ziarna RNG.

### 6.5. `BattleTickLoopTests`

Plik: `Assets/DeckBattle/Tests/EditMode/BattleTickLoopTests.cs`

Dodać lub dostosować testy:

- ukończony krok aktualizuje pozycję przed targetowaniem i walką,
- po śmierci blokującej jednostki pole staje się dostępne dla kolejnego planowania,
- śmierć celu powoduje wybór innego osiągalnego celu,
- `UnitMoved` jest generowane tylko dla zaakceptowanego ruchu,
- zablokowane jednostki nie wprowadzają niepoprawnego stanu symulacji.

## 7. Kolejność wdrożenia

1. Dodać testy charakteryzujące uzgodnione reguły zajętości i konfliktów.
2. Rozbudować bezalokacyjny pathfinding w `HexBoard`.
3. Zmienić `AttackPositionSelector`, aby zwracał pełny wynik ścieżki.
4. Zmienić `TargetSelector`, aby wybierał osiągalny cel i przekazywał wynik nawigacji.
5. Przebudować planowanie oraz konflikty w `MovementResolver`.
6. Uporządkować kolejność operacji w `BattleTickLoop`.
7. Uruchomić najpierw wąskie testy pathfindingu i ruchu, następnie cały zestaw Edit Mode.
8. Sprawdzić kompilację oraz testy przez Unity MCP.
9. Sprofilować reprezentatywną walkę pod kątem czasu ticka i `GC Alloc`.
10. Zweryfikować zachowanie w scenie `Battle` na docelowych formacjach.

## 8. Kryteria ukończenia

Rework jest ukończony, gdy:

- jednostka zawsze porusza się w stronę osiągalnej pozycji ataku wybranego celu,
- każdy krok wynika z najkrótszej aktualnie dostępnej ścieżki,
- ścieżka jest ponownie sprawdzana po każdym ukończonym hexie,
- żaden ruch nie przechodzi przez zajęte pole,
- jednostka bez osiągalnego przeciwnika pozostaje w miejscu,
- nie występują nakładające się pozycje ani podwójne rezerwacje,
- konflikty są deterministyczne,
- logika nie zależy od animacji ani obiektów sceny,
- odpowiednie testy Edit Mode przechodzą,
- gorąca ścieżka ruchu nie generuje alokacji pamięci,
- koszt ticka pozostaje stabilny dla reprezentatywnej liczby jednostek na planszy mobilnej.

## 9. Ryzyko zakleszczenia

Twarde traktowanie wszystkich zajętych pól jako przeszkód może utworzyć układ, w którym żadna jednostka nie ma osiągalnego przeciwnika. Zgodnie z ustalonymi regułami system ruchu pozostawi wtedy jednostki w miejscu.

Timeout, wykrywanie braku postępu lub zakończenie takiej walki remisem nie należą do zakresu tego reworku. Powinny zostać zdefiniowane jako osobna reguła przebiegu i kończenia walki.
