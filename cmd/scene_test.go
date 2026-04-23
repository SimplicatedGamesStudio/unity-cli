package cmd

import "testing"

func TestSceneCmd_CaptureObject(t *testing.T) {
	send, params := mockSend("capture_scene_object", t)

	_, err := sceneCmd([]string{"capture-object", "--path", "BossScene::Units/BossRoot[0]"}, send)
	if err != nil {
		t.Fatalf("sceneCmd returned error: %v", err)
	}

	if (*params)["path"] != "BossScene::Units/BossRoot[0]" {
		t.Fatalf("expected path to be forwarded")
	}
}
