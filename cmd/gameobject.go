package cmd

import (
	"fmt"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func gameObjectCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: unity-cli gameobject <info|list>")
	}

	switch args[0] {
	case "info":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("get_game_object_info", params)
	case "list":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("list_game_objects_in_hierarchy", params)
	default:
		return nil, fmt.Errorf("unknown gameobject action: %s\nAvailable: info, list", args[0])
	}
}
