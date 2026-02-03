#include <QGuiApplication>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include "controllers/ApplicationController.hpp"

int main(int argc, char *argv[])
{
    // High DPI scaling is enabled by default in Qt 6
    QGuiApplication app(argc, argv);

    // Set application metadata
    app.setOrganizationName("StreamDVR");
    app.setApplicationName("StreamDVR");

    // Initialize core controller
    ApplicationController appController;

    QQmlApplicationEngine engine;

    // Register the controller as a context property so QML can access it globally as "App"
    engine.rootContext()->setContextProperty("App", &appController);

    const QUrl url(u"qrc:/StreamDVR/src/ui/Main.qml"_qs);

    QObject::connect(&engine, &QQmlApplicationEngine::objectCreated,
                     &app, [url](QObject *obj, const QUrl &objUrl) {
        if (!obj && url == objUrl)
            QCoreApplication::exit(-1);
    }, Qt::QueuedConnection);

    engine.load(url);

    return app.exec();
}
