package main

import (
	"fmt"
	"os"

	"github.com/SimplicatedGamesStudio/unity-cli/cmd"
)

var Version = "dev"

func init() {
	cmd.Version = Version
}

func main() {
	if err := cmd.Execute(); err != nil {
		fmt.Fprintf(os.Stderr, "Error: %v\n", err)
		os.Exit(1)
	}
}
