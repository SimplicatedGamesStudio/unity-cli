package cmd

import "testing"

func TestUICmd_CaptureCanvas(t *testing.T) {
	send, params := mockSend("capture_ui_canvas", t)

	_, err := uiCmd([]string{"capture-canvas", "--path", "HUD/MainCanvas[0]", "--width", "2560"}, send)
	if err != nil {
		t.Fatalf("uiCmd returned error: %v", err)
	}

	if (*params)["path"] != "HUD/MainCanvas[0]" {
		t.Fatalf("expected path to be forwarded")
	}
	if (*params)["width"] != 2560 {
		t.Fatalf("expected width to be parsed as int")
	}
}

func TestUICmd_MissingSubcommand(t *testing.T) {
	send, _ := mockSend("capture_ui_canvas", t)

	if _, err := uiCmd(nil, send); err == nil {
		t.Fatal("expected usage error for missing subcommand")
	}
}
