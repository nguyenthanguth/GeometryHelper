// Reads a Claude Code hook's JSON from stdin and prints the tool's file path with forward slashes, or nothing.
let input = "";
process.stdin.on("data", (chunk) => (input += chunk));
process.stdin.on("end", () => {
    try {
        const path = JSON.parse(input).tool_input.file_path || "";
        process.stdout.write(path.split("\\").join("/"));
    } catch {
        // Not a hook payload with a file path: print nothing.
    }
});
