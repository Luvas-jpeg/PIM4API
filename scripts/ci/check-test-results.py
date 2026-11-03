"""Reject empty/skipped suites and require real PostgreSQL concurrency results."""

import sys
import xml.etree.ElementTree as ET


def validate(path):
    root = ET.parse(path).getroot()
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    results = root.findall("t:Results/t:UnitTestResult", ns)
    if not results or any(result.get("outcome") != "Passed" for result in results):
        raise ValueError("Every backend test must run and pass; skipped/empty suites are rejected.")
    concurrency_ids = set()
    for definition in root.findall("t:TestDefinitions/t:UnitTest", ns):
        method = definition.find("t:TestMethod", ns)
        if method is not None and method.get("className", "").split(",")[0] == "backend.Tests.PostgreSqlConcurrencyTests":
            concurrency_ids.add(definition.get("id"))
    executed = {result.get("testId") for result in results}
    if len(concurrency_ids) < 6 or not concurrency_ids.issubset(executed):
        raise ValueError("The six PostgreSQL concurrency tests must be present and executed.")
    print(f"{len(results)} backend tests passed; PostgreSQL concurrency verified.")


if __name__ == "__main__":
    try:
        validate(sys.argv[1])
    except (IndexError, OSError, ET.ParseError, ValueError) as error:
        print(f"Test result verification failed: {error}", file=sys.stderr)
        sys.exit(1)
