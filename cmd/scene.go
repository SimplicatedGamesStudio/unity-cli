package cmd

import (
	"fmt"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func sceneCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: unity-cli scene <capture-object>")
	}

	switch args[0] {
	case "capture-object":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("capture_scene_object", params)
	default:
		return nil, fmt.Errorf("unknown scene action: %s\nAvailable: capture-object", args[0])
	}
}
