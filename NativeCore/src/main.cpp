#include <QApplication>
#include "ui/main_window.hpp"

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);

    // Set style (Fusion is a good cross-platform base)
    app.setStyle("Fusion");

    MainWindow window;
    window.show();

    return app.exec();
}
