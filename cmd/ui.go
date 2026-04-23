package cmd

import (
	"fmt"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func uiCmd(args []string, send sendFn) (*client.CommandResponse, error) {
	if len(args) == 0 {
		return nil, fmt.Errorf("usage: unity-cli ui <capture-canvas>")
	}

	switch args[0] {
	case "capture-canvas":
		params, err := buildParams(args[1:], nil)
		if err != nil {
			return nil, err
		}
		return send("capture_ui_canvas", params)
	default:
		return nil, fmt.Errorf("unknown ui action: %s\nAvailable: capture-canvas", args[0])
	}
}
