import QtQuick
import QtQuick.Controls
import QtQuick.Layouts

ApplicationWindow {
    id: window
    width: 1280
    height: 720
    visible: true
    title: qsTr("StreamDVR")
    color: "#1e1e1e" // Dark theme base

    // Basic Layout
    RowLayout {
        anchors.fill: parent
        spacing: 0

        // Sidebar
        Rectangle {
            Layout.preferredWidth: 250
            Layout.fillHeight: true
            color: "#252526"

            ColumnLayout {
                anchors.fill: parent
                anchors.margins: 10

                Label {
                    text: "StreamDVR"
                    font.pixelSize: 24
                    font.bold: true
                    color: "white"
                    Layout.alignment: Qt.AlignHCenter
                }

                Item { Layout.fillHeight: true } // Spacer

                Label {
                    text: App.statusMessage
                    color: "gray"
                    Layout.alignment: Qt.AlignHCenter
                }
            }
        }

        // Main Content Area
        Rectangle {
            Layout.fillWidth: true
            Layout.fillHeight: true
            color: "#1e1e1e"

            ColumnLayout {
                anchors.centerIn: parent
                spacing: 20

                Label {
                    text: App.isRecording ? "Recording Active: " + App.activeGame : "Ready"
                    font.pixelSize: 32
                    color: App.isRecording ? "#ff4444" : "white"
                }

                RowLayout {
                    spacing: 20

                    Button {
                        text: "Start Recording"
                        enabled: !App.isRecording
                        onClicked: App.startRecording()
                    }

                    Button {
                        text: "Stop Recording"
                        enabled: App.isRecording
                        onClicked: App.stopRecording()
                    }

                    Button {
                        text: "Save Clip (30s)"
                        enabled: App.isRecording
                        onClicked: App.saveClip(30)
                    }
                }
            }
        }
    }
}
