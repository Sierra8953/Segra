#include "mpv_widget.hpp"
#include <QWindow>
#include <iostream>

MpvWidget::MpvWidget(QWidget *parent) : QWidget(parent)
{
    // Ensure we have a native window handle for MPV to attach to
    setAttribute(Qt::WA_NativeWindow);
    setAttribute(Qt::WA_OpaquePaintEvent);
    setAttribute(Qt::WA_NoSystemBackground);
    setAttribute(Qt::WA_DontCreateNativeAncestors);

    mpv = mpv_create();
    if (!mpv) {
        std::cerr << "failed to create mpv context" << std::endl;
        return;
    }

    // Embed via WID (Window ID)
    // On Windows with Qt6, winId() returns the HWND.
    int64_t wid = (int64_t)this->winId();
    mpv_set_option(mpv, "wid", MPV_FORMAT_INT64, &wid);

    // Enable simple Input
    mpv_set_option_string(mpv, "input-default-bindings", "yes");
    mpv_set_option_string(mpv, "input-vo-keyboard", "yes");

    if (mpv_initialize(mpv) < 0) {
        std::cerr << "mpv init failed" << std::endl;
    }
}

MpvWidget::~MpvWidget()
{
    if (mpv) mpv_terminate_destroy(mpv);
}

void MpvWidget::Load(const QString& path)
{
    if (!mpv) return;
    const char* cmd[] = {"loadfile", path.toUtf8().constData(), nullptr};
    mpv_command(mpv, cmd);
}

void MpvWidget::Play()
{
    if (!mpv) return;
    const char* cmd[] = {"set_property", "pause", "no", nullptr};
    mpv_command(mpv, cmd);
}

void MpvWidget::Pause()
{
    if (!mpv) return;
    const char* cmd[] = {"set_property", "pause", "yes", nullptr};
    mpv_command(mpv, cmd);
}

void MpvWidget::paintEvent(QPaintEvent *event)
{
    // Do nothing - MPV renders here.
    // If MPV is not running, we might want to draw black background.
}
