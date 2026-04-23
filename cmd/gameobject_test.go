package cmd

import "testing"

func TestGameObjectCmd_Info(t *testing.T) {
	send, params := mockSend("get_game_object_info", t)

	_, err := gameObjectCmd([]string{"info", "--prefab", "Assets/Prefabs/UI/MainHUD.prefab", "--path", "Root/MainCanvas[0]"}, send)
	if err != nil {
		t.Fatalf("gameObjectCmd returned error: %v", err)
	}

	if (*params)["prefab"] != "Assets/Prefabs/UI/MainHUD.prefab" {
		t.Fatalf("expected prefab to be forwarded")
	}
}

func TestGameObjectCmd_ListRecursive(t *testing.T) {
	send, params := mockSend("list_game_objects_in_hierarchy", t)

	_, err := gameObjectCmd([]string{"list", "--path", "HUD/MainCanvas[0]", "--recursive"}, send)
	if err != nil {
		t.Fatalf("gameObjectCmd returned error: %v", err)
	}

	if (*params)["recursive"] != true {
		t.Fatalf("expected recursive flag to be forwarded as bool")
	}
}
