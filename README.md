# DOKUMENTACJA PROJEKTOWA
### Połączenie z urządzeniem (serwerem OPC UA)
#### Sposób uruchomienia aplikacji
Aby uruchomić projekt,
1.  Upewnij się, że masz zainstalowane Visual Studio (wersja 2019 lub nowsza)
2.  Sklonuj repozytorium za pomocą Visual Studio lub wiersza poleceń (Git)
3.	Otwórz plik rozwiązania projektu - ```OpcAgent.sln```
4.	Upewnij się, że wszystkie wymagane pakiety są zainstalowane. W razie potrzeby kliknij Restore NuGet Packages
5.	Kliknij prawym przyciskiem myszy na Solution i wybierz Configure Startup Projects…
6.	Następnie wybierz Multiple startup Project i ustaw akcję Start dla projektów ```ServiceBus.Console``` oraz ```DeviceSdk.Console```
7.	Aplikację należy uruchomić przyciskiem „Start”.

#### Sposób połączenia z serwerem
Należy otworzyć plik ```sharedsettings.json``` i dodać adres url serwera OPC (najczęściej jest to adres ```opc.tcp://localhost:4840/```) pod kluczem ```ConnectionStrings –> OpcServer```. Na przykład:
```cSharp
{
  "ConnectionStrings": {
    "OpcServer": "opc.tcp://localhost:4840/",
    ...
  }
}
```

#### Sposób i częstotliwość odczytu i zapisu danych oraz wywoływania węzłów-metod
1.  Odczyt danych z serwera OPC
   
    Program pobiera dane wszystkich urządzeń połączonych z serwerem OPC. Aplikacja odczytuje następujące właściwości dla każdego urządzenia:
    ```
    DeviceName, ProductionStatus, ProductionRate, WorkorderId, Temperature, GoodCount, BadCount, DeviceError
    ```
2.	Zapis danych

    Wartości własności: ```DeviceName, ProductionStatus, WorkorderId, Temperature, GoodCount, BadCount``` każdego urządzenia są zapisywane do IoT Hub jako dane telemetryczne w formacie JSON.
  	
    Wartości  pozostałych własności (```ProductionRate i DeviceError```) dla każdego urządzenia są zapisywane w DeviceTwin we właściwościach raportowanych (ang. Reported Property).

    Dane z serwera OPC są odczytywane i zapisywane cyklicznie dla każdego urządzenia z opóźnieniem co 1 sekundę.
3.	Wywołanie węzłów-metod

    Możliwe jest użycie dwóch metod: ```EmergencyStop``` i ```ResetErrorStatus``` poprzez wywołanie metod bezpośrednich (DirectMethod) na danym urządzeniu w IoTHub.

### Konfiguracja agenta
#### Sposób, w jaki agent jest konfigurowany do komunikacji z danym serwerem OPC UA oraz instancją IoT Hub
Aby agent został poprawnie skonfigurowany, należy otowrzyć plik konfiguracyjny ```sharedsettings.json``` oraz uzupełnić klucze odpowiednimi wartościami. Przykładowo:
```cSharp
{
  "ConnectionStrings": {
    "OpcServer": "opc.tcp://localhost:4840/", //address URL of OPC UA server
    "Device 1": "HostName=IoTHub-UL.azure-devices.net;DeviceId=DeviceSdk1;SharedAccessKey=", //connection string to first device in iot hub in Azure
    "Device 2": "HostName=IoTHub-UL.azure-devices.net;DeviceId=DeviceSdk2;SharedAccessKey=", //connection string to second device in iot hub in Azure
    "Device 3": "HostName=IoTHub-UL.azure-devices.net;DeviceId=DeviceSdk3;SharedAccessKey=", //connection string to third device in iot hub in Azure
    "Service": "HostName=IoTHub-UL.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=", //connection string to iot hub owner service in Azure
    "ServiceBus": "Endpoint=sb://service-bus-ul.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=", //connection string to service bus in Azure
    "ProductionKpiQueueName": "production-kpi-queue", //name of ProductionKpi queue
    "DeviceErrorsQueueName": "device-errors-queue", //name of DeviceErrors queue
    "EmailCommunicationService": "endpoint=https://communication-service-ul.europe.communication.azure.com/;accesskey=" //connection string to email communication service in Azure
  },
  "EmailAddress": {
    "Sender": "DoNotReply@84b32d23-5969-498a-afba-44ce1ec3752a.azurecomm.net", //domain name of email communication service
    "Receiver": "example@gamil.com" //email address of the email recipient
  }
}
```

### Rodzaj, format i częstotliwość wiadomości (D2C messages) wysyłanych przez agenta do IoT Hub
Dla każdego urządzenia agent wysyła opisane poniżej rodzaje wiadomości do IoT Hub w formacie JSON.
1.	Dane telemetryczne
    
    Co 1 sekundę wysyłane są wiadomości zawierające informacje każdego urządzenia o: ```DeviceName, ProductionStatus, WorkorderId, Temperature, GoodCount, BadCount```.
    Przykładowa wiadomość na konsoli:
    ```
    Device 1 sending telemetry to IoTHub...
    18.01.2025 17:00:39 > Sending telemetry: { DeviceName = Device 1, ProductionStatus = 1, WorkorderId = e7a3177e-14de-44a6-9ab5-3eda78192159, Temperature = 64,2173060373713, GoodCount = 57, BadCount = 4 }
    ```
    Przykładowa telemetria na urządzeniu w IoTHub:
    ```
    Sat Jan 18 2025 17:04:29 GMT+0100 (czas środkowoeuropejski standardowy):
    {
      "body": {
        "DeviceName": "Device 1",
        "ProductionStatus": 1,
        "WorkorderId": "e7a3177e-14de-44a6-9ab5-3eda78192159",
        "Temperature": 886,
        "GoodCount": 219,
        "BadCount": 25
      },
      "enqueuedTime": "Sat Jan 18 2025 17:04:29 GMT+0100 (czas środkowoeuropejski standardowy)"
    }
    ```
2.	Informowanie o aktualnym stanie urządzenia

    W przypadku zmiany stanu błędów wysyłana jest pojedyncza wiadomość informująca o poprzednim i aktualnym stanie urządzenia.
    Przykładowa wiadomość na konsoli:
    ```
    Device1: DeviceError has changed from PowerFailure to PowerFailure, SensorFailure
    ```
    Przykładowa wiadomość na urządzeniu w IoTHub:
    ```
    Sat Jan 18 2025 17:09:18 GMT+0100 (czas środkowoeuropejski standardowy):
    {
      "body": {
        "Message": "DeviceError has changed from PowerFailure to PowerFailure, SensorFailure"
      },
      "enqueuedTime": "Sat Jan 18 2025 17:09:18 GMT+0100 (czas środkowoeuropejski standardowy)"
    }
    ```

3.	Zgłoszenie nowych błędów

    W przypadku pojawienia się błędu, wysyłana jest pojedyncza wiadomość informująca o tym IoTHub (wiadomość potrzebna do później opisanego procesu automatycznego wywoływania metody ```EmergencyStop```).
    Przykładowa wiadomość na urządzeniu w IoTHub:
    ```
    Sat Jan 18 2025 17:09:13 GMT+0100 (czas środkowoeuropejski standardowy):
    {
      "body": {
        "IsNewError": 1
      },
      "enqueuedTime": "Sat Jan 18 2025 17:09:13 GMT+0100 (czas środkowoeuropejski standardowy)"
    }
    ```

### Rodzaj i format danych przechowywanych w Device Twin
Device Twin każdego urządzenia przechowuje informacje o stanie urządzenia w formacie JSON, który można synchronizować między urządzeniem a chmurą.
Rodzaje danych przechowywanych w Device Twin:
* Desired Properties (właściwości pożądane):

    We właściwościach pożądanych można ustawić jeden rodzaj własności - ```ProductionRate```. Jest to docelowy wskaźnik produkcji, ustawiony przez IoT Hub (np. 50). Początkowo właściwość ta nie jest określona. Przy pierwszym uruchomieniu aplikacji należy ręcznie do klucza ```properties -> desired``` dodać linię:
    ```
    "properties": {
      "desired": {
        "ProductionRate": <OKREŚLONA_WARTOŚĆ>,
        ...
      }
    }
    ```
* Reported Properties (właściwości raportowane)

    We właściwościach raportowanych z urządzenia zapisywane są dwa rodzaje własności:
    -	```DeviceError```: Kod aktualnego błędu urządzenia (np. 4 odpowiada ```SensorFailure```)
    -	```ProductionRate```: Rzeczywisty wskaźnik produkcji raportowany przez urządzenie (np. 40).
  
    Dane te są raportowane z częstotliwością co 1 sekundę.

Przykładowa zawartość DeviceTwin:
```
{
	"deviceId": "DeviceSdk2",
	"etag": "AAAAAAAAAAk=",
	"deviceEtag": "NzUzNTQ2NDc3",
	"status": "enabled",
	"statusUpdateTime": "0001-01-01T00:00:00Z",
	"connectionState": "Connected",
	"lastActivityTime": "2025-01-19T13:30:36.4297358Z",
	"cloudToDeviceMessageCount": 0,
	"authenticationType": "sas",
	"x509Thumbprint": {
		"primaryThumbprint": null,
		"secondaryThumbprint": null
	},
	"modelId": "",
	"version": 2091,
	"properties": {
		"desired": {
			"ProductionRate": 30,
			"$metadata": {
				"$lastUpdated": "2025-01-19T13:35:12.1388554Z",
				"$lastUpdatedVersion": 9,
				"ProductionRate": {
					"$lastUpdated": "2025-01-19T13:35:12.1388554Z",
					"$lastUpdatedVersion": 9
				}
			},
			"$version": 9
		},
		"reported": {
			"DeviceError": 8,
			"ProductionRate": "0",
			"$metadata": {
				"$lastUpdated": "2025-01-19T13:35:11.7784566Z",
				"DeviceError": {
					"$lastUpdated": "2025-01-19T13:35:11.7784566Z",
					"Description": {
						"$lastUpdated": "2025-01-09T18:18:54.4868913Z"
					},
					"Value": {
						"$lastUpdated": "2025-01-09T18:18:54.4868913Z"
					}
				},
				"ProductionRate": {
					"$lastUpdated": "2025-01-19T13:35:11.7784566Z"
				}
			},
			"$version": 2082
		}
	},
	"capabilities": {
		"iotEdge": false
	}
}
```

### Dokumentacja Direct Methods zaimplementowanych w agencie
Program umożliwia wywołanie trzech metod bezpośrednich z urządzenia w IoTHub. Są to:
1. ```EmergencyStop```

    Jest to metoda zatrzymująca działanie urządzenia w przypadku:
    * Ręcznego wywołania funkcji (Invoke method) jako DirectMethod na urządzeniu w IoTHub:
      
      ![obraz](https://github.com/user-attachments/assets/8323d317-241b-4e1e-9216-5dff0d23dd0f)
    * Pojawienia się minimum 4 nowych błędów na urządzeniu w ciągu 1 minuty (opisane w dalszej części dokumentacji)

    W momencie wywołania metody urządzenie zatrzymuje swoje działanie (ustawia wartość ```ProductionStatus``` na 0) oraz kasuje wszystkie dotychczasowo ustawione błędy na urządzeniu na rzecz zaznaczenia opcji ```EmergencyStop```. 

    Przykładowa wiadomość na konsoli:

    ```
    METHOD EXECUTED: EmergencyStop on Device 1
    ```

    Przykładowy widok z symulatora:
    
      Przed wywołaniem metody:
      
      ![obraz](https://github.com/user-attachments/assets/10fecaf7-0f09-45eb-8d85-1ff8f547ebaf)
      
      Po wywołaniu metody:

      ![obraz](https://github.com/user-attachments/assets/849a0e29-007e-4be4-b0c8-447eeb1471ac)
    
    Aby kontynuować pracę urządzenia, należy wywołać bezpośrednią metodę ```ResetErrorStatus``` opisaną poniżej.

3. ```ResetErrorStatus```

    Jest to metoda kasująca wszystkie dotychczasowo ustawione błędy na urządzeniu (wraz z opcją ```EmergencyStop```) w przypadku ręcznego wywołania funkcji (Invoke method) jako DirectMethod na urządzeniu w IoTHub:
    
    ![obraz](https://github.com/user-attachments/assets/18441e36-591e-487e-95c5-358c0c7c37bf)

    Przykładowa wiadomość na konsoli:

    ```
    METHOD EXECUTED: ResetErrorStatus on Device 1
    ```

    Przykładowy widok z symulatora:
    
      Przed wywołaniem metody:
      
      ![obraz](https://github.com/user-attachments/assets/9bfb34d0-0601-4a04-b3ba-acae6fde3ecb)
    
      Po wywołaniu metody:
      
      ![obraz](https://github.com/user-attachments/assets/0b7d0200-cf44-43d2-a30e-1936cf66c9e7)

3. ```DefaultMethod```

    Jest to metoda wykonywana, gdy na urządzeniu w IoTHub zostanie wywołana metoda nieistniejąca. 

    ![obraz](https://github.com/user-attachments/assets/9c007fa5-0199-4b77-9b6c-6b274fc588c5)

    W przypadku wykonania takiej metody, na konsoli zostanie wyświetlona przykładowo poniższa informacja:
    
    ```
    UNKNOWN METHOD EXECUTED: NewMethod on Device 2
    ```

### Kalkulacje
Program umożliwia prowadzenie trzech rodzajów kalkulacji:

1. KPI Produkcji:

    Program oblicza stosunek dobrej produkcji do całkowitej wyrażony w procentach dla każdego urządzenia w 5-minutowych oknach czasowych. Schemat przetwarzania danych podczas kalkulacji KPI produkcji:
    ```
    Server OPC UA -> Aplikacja agenta -> IoTHub -> Azure Stream Analytics -> ServiceBus (/production-kpi-queue)
    ```
    Wartości właściwości każdego urządzenia (```GoodCount, BadCount```) są wysyłane jako dane telemetryczne do IoTHub w Azure. Następnie za pomocą specjalnego zapytania w usłudze ASA, wartości te zostają wykorzystane do obliczenia stosunku dobrej produkcji (```GoodCount```) do całkowitej (```GoodCount + BadCount```) w oknach 5-minutowych. Gdy obliczona wartość wynosi poniżej 90%, dane są przesyłane i przechowywane w kolejce ServiceBus Queue (```production-kpi-queue```). 
    
    Wykorzystane zapytanie w Stream Analytics Job:
    ```sql
    SELECT System.Timestamp() AS WindowEndTime, 
    IoTHub.ConnectionDeviceId AS DeviceId, (SUM(GoodCount)*100.0)/SUM(GoodCount+BadCount) AS ProcentOfGoodProduction 
    INTO [production-kpi-queue] 
    FROM [asa-iothub-ul] 
    GROUP BY IoTHub.ConnectionDeviceId, TumblingWindow(minute,5) 
    HAVING SUM(GoodCount) + SUM(BadCount) > 0 AND ProcentOfGoodProduction < 90;
    ```
    
    Przykładowa zawartość kolejki:
    ```
    {"WindowEndTime":"2025-01-18T16:05:00.0000000Z","DeviceId":"DeviceSdk2","ProcentOfGoodProduction":66.89088191330343}
    {"WindowEndTime":"2025-01-18T16:10:00.0000000Z","DeviceId":"DeviceSdk1","ProcentOfGoodProduction":87.69636011162208}
    {"WindowEndTime":"2025-01-18T16:10:00.0000000Z","DeviceId":"DeviceSdk2","ProcentOfGoodProduction":66.76983412271292}
    ```

2. Temperatura

    Program oblicza wartości średnią, minimalną i maksymalną temperaturę z ostatnich 5 minut, które są aktualizowane co minutę oraz grupowane według urządzenia. Schemat przetwarzania danych podczas kalkulacji temperatury:
    ```
    Server OPC UA -> Aplikacja agenta -> IoTHub -> Azure Stream Analytics -> Storage Account -> Containter (/temperature) -> Blobs
    ```
    
    Wartości właściwości każdego urządzenia (```Temperature```) są wysyłane jako dane telemetryczne do IoTHub w Azure. Następnie co 1 minutę za pomocą specjalnego zapytania w usłudze ASA, wartości te zostają wykorzystane do obliczenia średniej, minimalnej i maksymalnej temperatury z ostatnich 5 minut. Obliczone wartości są przechowywane w blobach znajdujących się w Azure Storage Account w specjalnym kontenterze ```/temperature```.
    
    Wykorzystane zapytanie w Stream Analytics Job:
    ```sql
    SELECT System.Timestamp() AS WindowEndTime, 
    IoTHub.ConnectionDeviceId, 
    MIN(Temperature) AS MinTemp, 
    MAX(Temperature) AS MaxTemp, 
    AVG(Temperature) AS AvgTemp 
    INTO [temperature] 
    FROM [asa-iothub-ul] 
    GROUP BY IoTHub.ConnectionDeviceId, HoppingWindow(minute,5,1);
    ```
    
    Przykładowa zawartość bloba:
    ```
    {"WindowEndTime":"2025-01-11T00:39:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":25.0,"MaxTemp":25.543191629397537,"AvgTemp":25.234729220074023}
    {"WindowEndTime":"2025-01-11T00:40:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-974.0,"MaxTemp":76.0,"AvgTemp":-68.72534183362018}
    {"WindowEndTime":"2025-01-11T00:41:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-974.0,"MaxTemp":781.0,"AvgTemp":-24.243315620867698}
    {"WindowEndTime":"2025-01-11T00:42:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-974.0,"MaxTemp":781.0,"AvgTemp":-37.759277363792826}
    {"WindowEndTime":"2025-01-11T00:43:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-974.0,"MaxTemp":781.0,"AvgTemp":-37.759277363792826}
    {"WindowEndTime":"2025-01-11T00:44:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-974.0,"MaxTemp":781.0,"AvgTemp":-54.71920221329544}
    {"WindowEndTime":"2025-01-11T00:45:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-971.0,"MaxTemp":781.0,"AvgTemp":-0.6}
    {"WindowEndTime":"2025-01-11T00:46:00.0000000Z","ConnectionDeviceId":"DeviceSdk1","MinTemp":-561.0,"MaxTemp":542.0,"AvgTemp":-135.75}
    ```

3.	Błędy urządzeń

    Program zlicza ilość pojawiających się błędów z ostatniej minuty. Jeśli ta ilość przekroczy wartość 3, dane są przesyłane i przechowywane w kolejce ServiceBus Queue (```/device-errors-queue```). Schemat przetwarzania danych podczas kalkulacji błędów urządzenia:
    ```
    Server OPC UA -> Aplikacja agenta -> IoTHub -> Azure Stream Analytics -> ServiceBus (/device-errors-queue)
    ```
    
    W momencie pojawienia się nowego błędu na urządzeniu, wysyłana jest pojedyncza wiadomość do IoTHub. Program za pomocą specjalnego zapytania w usłudze ASA, zlicza ilość tych wiadomości w ciągu ostatniej minuty. Gdy obliczona suma wynosi powyżej 3, dane są przesyłane i przechowywane w kolejce ServiceBus Queue (```/device-errors-queue```).
    
    Wykorzystane zapytanie w Stream Analytics Job:
    ```sql
    SELECT System.Timestamp() AS WindowEndTime, 
    IoTHub.ConnectionDeviceId AS DeviceId, 
    SUM(IsNewError) as OccuredErrors 
    INTO [device-errors-queue] 
    FROM [asa-iothub-ul] 
    WHERE IsNewError is not null 
    GROUP BY IoTHub.ConnectionDeviceId, SlidingWindow(minute,1) 
    HAVING SUM(IsNewError) > 3;
    ```
    
    Przykładowa zawartość kolejki:
    ```
    {"WindowEndTime":"2025-01-18T16:05:34.8410000Z","DeviceId":"DeviceSdk1","OccuredErrors":4.0}
    ```

### Logika biznesowa
Program implementuje logikę biznesową w 3 różnych sytuacjach:

1.	Obniżenie wartości ProductionRate

    Na podstawie danych zapisanych w kolejce ServiceBus Queue (production-kpi-queue) dotyczących kalkulacji KPI produkcji (kiedy KPI < 90%), program wywołuje funkcję, która obniża wartość ProductionRate o 10 dla danego urządzenia.
    
    W wyniku tego procesu wyświetlana jest informacja na konsoli: 
    ```
    Received message:
             {"WindowEndTime":"2025-01-18T16:05:00.0000000Z","DeviceId":"DeviceSdk2","ProcentOfGoodProduction":66.89088191330343}
    ProductionRate has been decreased by 10
    ```

2.	Wywołanie metody EmergencyStop

    Na podstawie danych zapisanych w kolejce ServiceBus Queue (device-errors-queue) dotyczących ilości pojawiających się błędów z ostatniej minuty (powyżej 3 błędów), program wywołuje metodę bezpośrednią EmergencyStop, która zatrzymuje działanie urządzenia. 
    
    W wyniku tego procesu wyświetlana jest informacja na konsoli:
    ```
    Received message: {"WindowEndTime":"2025-01-18T16:05:34.8410000Z","DeviceId":"DeviceSdk1","OccuredErrors":4.0}
    EmergencyStop invoked.
    ```

3.	Wysłanie maila z informacją o pojawieniu się nowego błędu

    W momencie pojawienia się nowego błędu na urządzeniu, program wysyła maila do określonego w pliku konfiguracyjnym (```sharedsettings.json```) odbiorcy z informacją jaki rodzaj błędu wystąpił i na którym urządzeniu. Schemat przetwarzania danych podczas wystąpienia błędu:
    ```
    Server OPC UA -> Aplikacja agenta -> Azure Communication Services -> Email Communication Services -> Email
    ```
    
    Każdorazowo przy pojawieniu się nowego błędu na urządzeniu, program łączy się ze specjalnymi usługami Azure ACS i ECS, które umożliwiają wysyłanie maila na konkretny adres mailowy.
    
    Przykładowa treść maila:
    ![Zrzut ekranu 2025-01-18 181436](https://github.com/user-attachments/assets/790ffcf4-7447-452b-a7d4-018731296e5b)
    
    Wiadomość o wysłaniu maila wyświetlana na konsoli:
    ```
  	Email has been sent
    ```
