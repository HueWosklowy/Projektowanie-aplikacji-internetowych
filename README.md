# Projektowanie-aplikacji-internetowych
System Telemetryczny z Kolejkowaniem, Walidacją i Notyfikacjami

📘 Projekt laboratoryjny

System Telemetryczny z Kolejkowaniem, Walidacją i Notyfikacjami

Instrukcja dla studentów

Prowadzący: Arkadiusz

🎯 Cel projektu

Celem projektu jest stworzenie kompletnego, odpornego na błędy systemu telemetrycznego, który:

generuje i wysyła dane pomiarowe,

przetwarza je asynchronicznie,

waliduje integralność danych,

zapisuje je do bazy czasowej (InfluxDB),

generuje alerty,

prezentuje powiadomienia w czasie rzeczywistym.

Projekt symuluje realne środowisko IoT, w którym dane muszą być niezawodne, a architektura odporna na błędy.

🧩 Architektura systemu
System składa się z trzech aplikacji oraz scenariusza testowego:

[Front API] → [RabbitMQ] → [Worker] → [InfluxDB] → [Webhook] → [SignalR WebApp] → [UI]

1️⃣ Aplikacja frontowa (API)

Zadania:

przyjmuje dane pomiarowe w formacie JSON, payload Base64

loguje treść żądania (middleware),

dekoduje Base64,

wysyła wiadomości do kolejki RabbitMQ,

obsługuje wiele kanałów publikacji

Studenci mają zrozumieć:

jak działa API przyjmujące dane,

jak wygląda logowanie requestów,

jak działa publikacja wiadomości do kolejki.

2️⃣ Aplikacja przetwarzająca (Worker)

Zadania:

odbiera wiadomości z kolejki,

dekoduje i waliduje integralność danych,

obsługuje błędy (Dead Letter Queue),

zapisuje poprawne dane do InfluxDB,

Studenci mają zrozumieć:

jak działa konsument kolejki,

czym jest dead-letter queue,

jak wygląda walidacja integralności danych,

jak działa zapis do bazy czasowej.

3️⃣ Aplikacja webowa (SignalR) + Alert Influx

Zadania:

konfiguracja alertu w influx

odbiera webhooki z InfluxDB,

przekazuje alerty do klientów w czasie rzeczywistym,

wyświetla powiadomienia w UI.

Studenci mają zrozumieć:

jak działa mechanizm webhooków,

jak działa SignalR,

jak budować prosty interfejs realtime.

4️⃣ Testy wydajnościowe (JMeter)

Zadania:

symulacja wielu urządzeń wysyłających dane,

testowanie API pod obciążeniem,

analiza stabilności kolejki i workerów,

generowanie raportów z testów.

Scenariusz JMeter obejmuje:

grupę wątków,

odczyt danych z CSV,

wysyłanie żądań POST do API,

losowe opóźnienia (Gaussian Timer),

wizualizację wyników (Table + Tree

🧠 Wymagania niefunkcjonalne

Odporność na błędy – system nie może się zatrzymać po błędnej wiadomości.

Modularność – trzy niezależne aplikacje współpracujące przez kolejkę.

Czytelność logów – każdy etap musi logować operacje.

Realizm – projekt ma symulować prawdziwy system telemetryczny.

Skalowalność – worker powinien obsługiwać wiele kanałów/konsumentów.

Testowalność – API musi być możliwe do obciążenia JMeterem.

📦 Zakres prac dla studentów

🔹 1. Przygotowanie środowiska

Studenci powinni:

uruchomić RabbitMQ (z panelem management),

uruchomić InfluxDB,

skonfigurować webhook w InfluxDB,

przygotować trzy aplikacje w osobnych projektach,

skonfigurować routing, porty i zależności.

🔹 2. Implementacja przepływu danych

Studenci mają odtworzyć przepływ:

Front API → RabbitMQ → Worker → InfluxDB → Webhook → SignalR → UI

Każdy etap musi być przetestowany osobno i w całości.

🔹 3. Walidacja integralności danych

Studenci mają:

zrozumieć ideę sumy kontrolnej,

wykonać walidację po stronie konsumenta,

obsłużyć przypadki błędne (DLQ),

zaprezentować przykłady poprawnych i błędnych wiadomości.

🔹 4. Obsługa alertów
Studenci mają:

przygotować regułę alertową w InfluxDB,

odebrać webhook,

przesłać alert do klientów przez SignalR,

wyświetlić go w UI.

🔹 5. Testy wydajnościowe (JMeter)
Studenci mają:

przygotować plik CSV z danymi wejściowymi,

uruchomić test JMeter,

przeanalizować wyniki (czas odpowiedzi, błędy, throughput),

przygotować raport z testów.

📄 Oczekiwane rezultaty

Studenci powinni dostarczyć:

✔ Dokumentację:

url repozytorim na publicznym githubie

dokumentacje projektową (opis poniżej)

wszystkie pliki (cała zawartość projektu)

✔ Działającą demonstrację:

wysyłanie danych z API,

odbiór i przetwarzanie w workerze,

zapis do InfluxDB,

generowanie alertów,

realtime UI z powiadomieniami.

✔ Prezentację końcową:

omówienie architektury,

analiza błędów i DLQ,

wnioski dotyczące niezawodności systemów rozproszonych.


👥 Zasady pracy zespołowej
Zespół 2–3 osoby.

Każdy członek odpowiada za jedną aplikację.

Wspólna integracja i testy.

W prezentacji każdy omawia swoją część.

📘 Dokumentacja końcowa projektu

System Telemetryczny z Kolejkowaniem, Walidacją i Notyfikacjami

Wymagania dotyczące zawartości dokumentacji

Dokumentacja końcowa musi potwierdzać wszystkie efekty uczenia się przewidziane w sylabusie.

Poniżej znajduje się pełna lista elementów, które muszą zostać uwzględnione.

1. Opis architektury systemu
diagram architektury mikroserwisowej (API → MQ → Worker → DB → Webhook → SignalR → UI)
opis ról poszczególnych komponentów
opis przepływu danych krok po kroku
uzasadnienie wyboru technologii (REST API, RabbitMQ, InfluxDB, SignalR)
opis mechanizmów skalowania systemu
2. Specyfikacja REST API
lista endpointów wraz z opisem
format danych wejściowych i wyjściowych
przykłady poprawnych i błędnych żądań
opis walidacji danych po stronie API
opis logowania żądań (middleware)
3. Kodowanie danych i walidacja integralności
opis procesu kodowania Base64
struktura payloadu
opis generowania i weryfikacji checksum (HMAC)
przykłady poprawnych i niepoprawnych checksum
opis obsługi błędów związanych z integralnością danych
4. Komunikacja przez kolejkę RabbitMQ
opis konfiguracji kolejki głównej
opis konfiguracji DLQ (dead-letter queue)
opis mechanizmu ACK/NACK
opis strategii obsługi błędów
przykłady wiadomości trafiających do DLQ
5. Worker – logika przetwarzania danych
opis procesu odbioru wiadomości
opis dekodowania i walidacji danych
opis obsługi wyjątków
opis logowania i monitorowania
przykłady przetworzonych wiadomości
6. Integracja z InfluxDB
opis konfiguracji bazy
format danych (Line Protocol)
przykładowe wpisy w bazie
opis zapytań Flux użytych do analizy danych
opis reguł alertowych
7. Alerty i obsługa webhooków
konfiguracja webhooka w InfluxDB
przykładowy payload alertu
opis logiki obsługi alertu w aplikacji webowej
opis integracji z SignalR
przykład wiadomości wysyłanej do UI
8. Aplikacja webowa – wizualizacja i notyfikacje
opis działania SignalR
opis mechanizmu powiadomień w czasie rzeczywistym
zrzuty ekranu z działania aplikacji
opis sposobu prezentacji alertów
9. Testy – jednostkowe, integracyjne i wydajnościowe
opis testów jednostkowych
opis testów integracyjnych (API → MQ → Worker → DB)
scenariusz testów JMeter
wyniki testów (wykresy, statystyki, analiza)
wnioski dotyczące wydajności i stabilności systemu
10. Aspekty bezpieczeństwa
opis walidacji danych
opis zabezpieczenia integralności (checksum)
identyfikacja potencjalnych zagrożeń
opis sposobów minimalizacji ryzyka
refleksja dotycząca etyki i jakości kodu
11. Praca zespołowa i organizacja projektu
podział ról w zespole
opis odpowiedzialności członków
opis procesu pracy (Git flow, code review)
samoocena i ocena współpracy
wnioski dotyczące pracy zespołowej
12. Podsumowanie i wnioski końcowe
co działa dobrze
co można poprawić
czego zespół się nauczył
jakie technologie warto rozwijać dalej
13. Załączniki
link do repozytorium GitHub
instrukcja uruchomienia projektu
pliki konfiguracyjne (np. docker-compose, .env)
przykładowe payloady testowe
wszystkie kody źródłowe w formie dokumentu txt
zip z projektem

✔ Dokumentacja musi być kompletna, spójna i przygotowana w formacie docx

Wszelkie zwolnienia załatwiamy do tygodnia od rozpoczęcia zajęć.

Możliwa nieobecność na 1 bloku zajęć (nie ostatnich)

Na ostatnich zajęciach prezentacja projektów i wpisywanie zaliczeń
