# DOKUMENTACJA PROJEKTOWA
### Połączenie z urządzeniem (serwerem OPC UA)
#### Sposób uruchomienia aplikacji
Aby uruchomić projekt,
1.  upewnij się, że masz zainstalowane Visual Studio (wersja 2019 lub nowsza)
2.  sklonuj repozytorium za pomocą Visual Studio lub wiersza poleceń (Git)
3.	otwórz plik rozwiązania projektu - ```OpcAgent.sln```
4.	upewnij się, że wszystkie wymagane pakiety są zainstalowane. W razie potrzeby kliknij Restore NuGet Packages
5.	kliknij prawym przyciskiem myszy na Solution i wybierz Configure Startup Projects…
6.	następnie wybierz Multiple startup Project i ustaw akcję Start dla projektów ```ServiceBus.Console``` oraz ```DeviceSdk.Console```
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
1.  <b>Odczyt danych z serwera OPC</b> <br/>
    Program pobiera dane wszystkich urządzeń połączonych z serwerem OPC. Aplikacja odczytuje następujące właściwości dla każdego urządzenia:
```
    DeviceName, ProductionStatus, ProductionRate, WorkorderId, Temperature, GoodCount, BadCount, DeviceError
```
2.	<b>Zapis danych</b> <br/>
    Wartości własności: ```DeviceName, ProductionStatus, WorkorderId, Temperature, GoodCount, BadCount``` każdego urządzenia są zapisywane do IoT Hub jako dane telemetryczne w formacie JSON. <br/>
  	Wartości  pozostałych własności (```ProductionRate i DeviceError```) dla każdego urządzenia są zapisywane w DeviceTwin we właściwościach raportowanych (ang. Reported Property). <br/>
  	Dane z serwera OPC są odczytywane i zapisywane cyklicznie dla każdego urządzenia z opóźnieniem co 1 sekundę. <br/>
3.	<b>Wywołanie węzłów-metod</b>
    Możliwe jest użycie dwóch metod: ```EmergencyStop``` i ```ResetErrorStatus``` poprzez wywołanie metod bezpośrednich (DirectMethod) na danym urządzeniu w IoTHub.

### Konfiguracja agenta
#### Sposób, w jaki agent jest konfigurowany do komunikacji z danym serwerem OPC UA oraz instancją IoT Hub
Aby agent został poprawnie skonfigurowany, należy otowrzyć plik konfiguracyjny ```sharedsettings.json``` oraz uzupełnić klucze odpowiednimi wartościami. Przykładowo:
```cSharp
{
  "ConnectionStrings": {
    "OpcServer": "opc.tcp://localhost:4840/",
    "Device 1": "HostName=IoTHub-UL.azure-devices.net;DeviceId=DeviceSdk1;SharedAccessKey=",
    "Device 2": "HostName=IoTHub-UL.azure-devices.net;DeviceId=DeviceSdk2;SharedAccessKey=",
    "Device 3": "HostName=IoTHub-UL.azure-devices.net;DeviceId=DeviceSdk3;SharedAccessKey=",
    "Service": "HostName=IoTHub-UL.azure-devices.net;SharedAccessKeyName=iothubowner;SharedAccessKey=",
    "ServiceBus": "Endpoint=sb://service-bus-ul.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=",
    "ProductionKpiQueueName": "production-kpi-queue",
    "DeviceErrorsQueueName": "device-errors-queue",
    "EmailCommunicationService": "endpoint=https://communication-service-ul.europe.communication.azure.com/;accesskey="
  },
  "EmailAddress": {
    "Sender": "DoNotReply@84b32d23-5969-498a-afba-44ce1ec3752a.azurecomm.net",
    "Receiver": "example@gmail.com"
  }
}
```

### Rodzaj, format i częstotliwość wiadomości (D2C messages) wysyłanych przez agenta do IoT Hub
Dla każdego urządzenia agent wysyła opisane poniżej rodzaje wiadomości do IoT Hub w formacie JSON.
1.	Dane telemetryczne <br/>
    Co 1 sekundę wysyłane są wiadomości zawierające informacje każdego urządzenia o: ```DeviceName, ProductionStatus, WorkorderId, Temperature, GoodCount, BadCount```. Przykładowo:
  	```
    ```
2.	Informowanie o aktualnym stanie <br/>
    W przypadku zmiany stanu błędów wysyłana jest pojedyncza wiadomość informująca o poprzednim i aktualnym stanie urządzenia. Przykładowo:
  	```
    ```
3.	Zgłoszenie nowych błędów <br/>
    W przypadku pojawienia się błędu, wysyłana jest pojedyncza wiadomość informująca o tym IoTHub (wiadomość potrzebna do później opisanego procesu automatycznego wywoływania metody ```EmergencyStop```). Przykładowo: 
    ```
    ```
