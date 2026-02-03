#pragma once
#include <QMainWindow>
#include <QPushButton>
#include <QVBoxLayout>
#include "mpv_widget.hpp"
#include "../core/obs_manager.hpp"

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    MainWindow(QWidget *parent = nullptr);
    ~MainWindow();

private slots:
    void onStartRecording();
    void onStopRecording();

private:
    MpvWidget* mpvPlayer;
    QPushButton* btnStart;
    QPushButton* btnStop;
    ObsManager* obsManager;
};
