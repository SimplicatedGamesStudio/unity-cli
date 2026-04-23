package cmd

import (
	"bytes"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"os"
	"path/filepath"
	"strings"
	"time"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

type suppressWriter struct {
	w        io.Writer
	suppress string
}

type testProgress struct {
	CurrentTest      string `json:"currentTest"`
	LastFinishedTest string `json:"lastFinishedTest"`
	Completed        int    `json:"completed"`
	Total            int    `json:"total"`
}

func (s *suppressWriter) Write(p []byte) (int, error) {
	if bytes.Contains(p, []byte(s.suppress)) {
		return len(p), nil
	}
	return s.w.Write(p)
}

func testCmd(args []string, send sendFn, port int, timeoutMs int) (*client.CommandResponse, error) {
	flags := parseSubFlags(args)

	mode := "EditMode"
	if m, ok := flags["mode"]; ok {
		mode = m
	}

	if mode != "EditMode" && mode != "PlayMode" {
		return nil, fmt.Errorf("--mode must be EditMode or PlayMode, got: %s", mode)
	}

	params := map[string]interface{}{
		"mode":       mode,
		"timeout_ms": timeoutMs,
	}
	if filter, ok := flags["filter"]; ok {
		params["filter"] = filter
	}

	resp, err := send("run_tests", params)
	if err != nil {
		return nil, err
	}

	if !resp.Success && strings.Contains(resp.Message, "Unknown command") {
		return nil, fmt.Errorf(
			"'run_tests' is not available.\n" +
				"Install the Unity Test Framework package:\n" +
				"  Window > Package Manager > search 'Test Framework' > Install")
	}

	if resp.Message != "running" {
		return resp, nil
	}

	fmt.Fprintln(os.Stderr, "Tests running, waiting for results...")

	// Suppress "Unsolicited response received on idle HTTP channel" during domain reload
	original := log.Writer()
	log.SetOutput(&suppressWriter{w: os.Stderr, suppress: "Unsolicited response received on idle HTTP channel"})
	defer log.SetOutput(original)

	return pollTestResults(port, timeoutMs)
}

func pollTestResults(port int, timeoutMs int) (*client.CommandResponse, error) {
	home, err := os.UserHomeDir()
	if err != nil {
		return nil, fmt.Errorf("cannot determine home directory: %w", err)
	}

	resultsPath := filepath.Join(home, ".unity-cli", "status", fmt.Sprintf("test-results-%d.json", port))
	progressPath := filepath.Join(home, ".unity-cli", "status", fmt.Sprintf("test-progress-%d.json", port))
	if timeoutMs <= 0 {
		timeoutMs = 120000
	}

	deadline := time.Now().Add(time.Duration(timeoutMs) * time.Millisecond)

	for time.Now().Before(deadline) {
		time.Sleep(500 * time.Millisecond)

		data, err := os.ReadFile(resultsPath)
		if err == nil {
			_ = os.Remove(resultsPath)
			_ = os.Remove(progressPath)
			var resp client.CommandResponse
			if err := json.Unmarshal(data, &resp); err != nil {
				return nil, fmt.Errorf("failed to parse test results: %w", err)
			}
			return &resp, nil
		}

		// Check Unity process is still alive (heartbeat may be stale during domain reload)
		inst, err := readStatus(port)
		if err == nil && inst.State == "stopped" {
			return nil, fmt.Errorf("unity editor has stopped (port %d)", port)
		}
	}

	if resp, ok, err := tryReadTestResults(resultsPath, progressPath); ok || err != nil {
		return resp, err
	}

	return nil, fmt.Errorf("timed out waiting for test results (%dms)%s", timeoutMs, readTestProgressSuffix(progressPath))
}

func tryReadTestResults(resultsPath string, progressPath string) (*client.CommandResponse, bool, error) {
	data, err := os.ReadFile(resultsPath)
	if err != nil {
		return nil, false, nil
	}

	_ = os.Remove(resultsPath)
	_ = os.Remove(progressPath)
	var resp client.CommandResponse
	if err := json.Unmarshal(data, &resp); err != nil {
		return nil, true, fmt.Errorf("failed to parse test results: %w", err)
	}
	return &resp, true, nil
}

func readTestProgressSuffix(path string) string {
	data, err := os.ReadFile(path)
	if err != nil {
		return ""
	}

	var progress testProgress
	if err := json.Unmarshal(data, &progress); err != nil {
		return ""
	}

	var parts []string
	if progress.CurrentTest != "" {
		parts = append(parts, "current test: "+progress.CurrentTest)
	}
	if progress.LastFinishedTest != "" {
		parts = append(parts, "last finished: "+progress.LastFinishedTest)
	}
	if progress.Total > 0 || progress.Completed > 0 {
		parts = append(parts, fmt.Sprintf("completed: %d/%d", progress.Completed, progress.Total))
	}
	if len(parts) == 0 {
		return ""
	}

	return "; " + strings.Join(parts, "; ")
}
