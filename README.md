Для того, чтобы использовать arcface_async_vectorizer nuget package локально на Mac OS или Linux
(без отправки на сервер с nuget пакетами) необходимо выполнить следующие действия:
  * 1. скачайте ML https://github.com/onnx/models/blob/main/vision/body_analysis/arcface/model/arcfaceresnet100-8.onnx
  * 2. поместите модель в директорию arcface_async_vectorizer
  * 3. внутри директории arcface_async_vectorizer выполнить команду
        ```sh
        dotnet pack
        ```
  * 4. после выполнения команды будет указан абсолютный путь к nuget пакету на вашем ПК,
        из этого пути нам нужно взять путь до директории с созданным пакетом
        (Пример: после выполнения команды отобразился пусь __/path/package_name.1.0.0.nupkg__,
        нам нужен только путь до директории __/path__)
  * 5. далее необходимо выполнить команду
        ```sh
        dotnet nuget add source --name arcface_async_vectorizer {Путь до директории из предыдущего пункта}
        ```
  * 6. после этого переходим в директорию проекта, к которму хотим выполнить подключение nuget пакета,
        выполняем команду для подключения пакета:
        ```sh
        dotnet add package arcface_async_vectorizer
        ```
