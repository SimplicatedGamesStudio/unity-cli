package cmd

import (
	"os"
	"path/filepath"
	"strings"
	"testing"

	"github.com/SimplicatedGamesStudio/unity-cli/internal/client"
)

func TestTestCmd_PassesTimeoutToConnector(t *testing.T) {
	var captured map[string]interface{}
	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		if command != "run_tests" {
			t.Fatalf("command = %q, want run_tests", command)
		}
		var ok bool
		captured, ok = params.(map[string]interface{})
		if !ok {
			t.Fatalf("params type = %T, want map[string]interface{}", params)
		}
		return &client.CommandResponse{Success: true}, nil
	}

	if _, err := testCmd([]string{"--filter", "SomeTests"}, send, 8090, 7000); err != nil {
		t.Fatalf("testCmd returned error: %v", err)
	}

	if captured["timeout_ms"] != 7000 {
		t.Fatalf("timeout_ms = %v, want 7000", captured["timeout_ms"])
	}
}

func TestTestCmd_PollsRunningResponseForEditMode(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)

	resultsDir := filepath.Join(home, ".unity-cli", "status")
	if err := os.MkdirAll(resultsDir, 0o755); err != nil {
		t.Fatalf("create results dir: %v", err)
	}

	resultsPath := filepath.Join(resultsDir, "test-results-8090.json")
	resultsJSON := `{"success":true,"message":"All 0 test(s) passed.","data":{"total":0,"passed":0,"failed":0,"skipped":0,"failures":[],"passes":[]}}`
	if err := os.WriteFile(resultsPath, []byte(resultsJSON), 0o644); err != nil {
		t.Fatalf("write results file: %v", err)
	}

	send := func(command string, params interface{}) (*client.CommandResponse, error) {
		return &client.CommandResponse{Success: true, Message: "running"}, nil
	}

	resp, err := testCmd([]string{"--mode", "EditMode", "--filter", "DefinitelyNoSuchTest"}, send, 8090, 7000)
	if err != nil {
		t.Fatalf("testCmd returned error: %v", err)
	}

	if resp.Message != "All 0 test(s) passed." {
		t.Fatalf("message = %q, want result file message", resp.Message)
	}
}

func TestPollTestResults_TimeoutIncludesProgressFileContext(t *testing.T) {
	home := t.TempDir()
	t.Setenv("HOME", home)

	resultsDir := filepath.Join(home, ".unity-cli", "status")
	if err := os.MkdirAll(resultsDir, 0o755); err != nil {
		t.Fatalf("create results dir: %v", err)
	}

	progressPath := filepath.Join(resultsDir, "test-progress-8090.json")
	progressJSON := `{"currentTest":"SlowTests.HangsForever","lastFinishedTest":"SlowTests.Previous","completed":42,"total":100}`
	if err := os.WriteFile(progressPath, []byte(progressJSON), 0o644); err != nil {
		t.Fatalf("write progress file: %v", err)
	}

	_, err := pollTestResults(8090, 1)
	if err == nil {
		t.Fatal("pollTestResults returned nil error, want timeout")
	}

	got := err.Error()
	for _, want := range []string{
		"timed out waiting for test results",
		"current test: SlowTests.HangsForever",
		"last finished: SlowTests.Previous",
		"completed: 42/100",
	} {
		if !strings.Contains(got, want) {
			t.Fatalf("timeout error = %q, want substring %q", got, want)
		}
	}
}
