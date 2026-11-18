// Creates the emulator-only match report key (never used in production).
const fs = require("fs");
if (!fs.existsSync(".secret.local")) {
  fs.writeFileSync(".secret.local", "MATCH_REPORT_KEY=dev-report-key\n");
}
