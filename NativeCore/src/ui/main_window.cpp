#include "main_window.hpp"
#include <QHBoxLayout>
#include <QMessageBox>

MainWindow::MainWindow(QWidget *parent)
    : QMainWindow(parent)
{
    setWindowTitle("StreamDVR");
    resize(1280, 720);

    // Central Widget
    QWidget* centralWidget = new QWidget(this);
    setCentralWidget(centralWidget);

    // Layout
    QVBoxLayout* layout = new QVBoxLayout(centralWidget);
    layout->setContentsMargins(0, 0, 0, 0);

    // Player
    mpvPlayer = new MpvWidget(this);
    mpvPlayer->setSizePolicy(QSizePolicy::Expanding, QSizePolicy::Expanding);
    layout->addWidget(mpvPlayer);

    // Controls
    QHBoxLayout* controlsLayout = new QHBoxLayout();
    layout->addLayout(controlsLayout);

    btnStart = new QPushButton("Start Recording", this);
    btnStop = new QPushButton("Stop Recording", this);

    controlsLayout->addWidget(btnStart);
    controlsLayout->addWidget(btnStop);

    // Logic
    obsManager = new ObsManager();
    if (!obsManager->Initialize()) {
        QMessageBox::critical(this, "Error", "Failed to initialize OBS backend.");
    }

    connect(btnStart, &QPushButton::clicked, this, &MainWindow::onStartRecording);
    connect(btnStop, &QPushButton::clicked, this, &MainWindow::onStopRecording);
}

MainWindow::~MainWindow()
{
    delete obsManager;
}

void MainWindow::onStartRecording()
{
    if (obsManager->StartRecording("recording.mp4")) {
        // In fMP4 mode, we can load the file immediately
        // MpvWidget handles loading asynchronously if needed
        mpvPlayer->Load("recording.mp4");
    } else {
        QMessageBox::warning(this, "Error", "Failed to start recording.");
    }
}

void MainWindow::onStopRecording()
{
    obsManager->StopRecording();
}
