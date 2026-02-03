#pragma once
#include <QWidget>
#include <mpv/client.h>

class MpvWidget : public QWidget
{
    Q_OBJECT

public:
    MpvWidget(QWidget *parent = nullptr);
    ~MpvWidget();

    void Load(const QString& path);
    void Play();
    void Pause();

protected:
    // Important: We override paintEvent to prevent Qt from drawing over MPV
    // But mainly we assume MPV draws directly to the HWND.
    // For Qt6, we might need QWindow or use 'setAttribute(Qt::WA_NativeWindow)'.
    void paintEvent(QPaintEvent *event) override;

private:
    mpv_handle* mpv = nullptr;
};
