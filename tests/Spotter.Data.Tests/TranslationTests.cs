using System.Text.Json.Nodes;
using Spotter.Data.Http;
using Spotter.Data.TarkovDev;

namespace Spotter.Data.Tests;

public class TranslationTests
{
    [Fact]
    public void Collects_the_properties_the_paths_end_in()
    {
        var names = JsonTranslator.TranslatableProperties(
        [
            "$.data.tasks.*.name",
            "$.data.tasks.*.objectives[*].description",
            "$.data.tasks.*.objectives[*]..bodyParts[*]",
            "$.data.tasks.*.objectives[*]['healthEffect','playerHealthEffect','enemyHealthEffect'].effects[*]",
            "$.data.maps.*.extracts[*].name",
            "$.data.*.name",
        ]);
        Assert.Equal(new[] { "bodyParts", "description", "effects", "name" }, names.Order().ToArray());
    }

    [Fact]
    public void Translates_values_but_never_ids()
    {
        // Objective descriptions are keyed by the objective's own id: the id must survive.
        var node = JsonNode.Parse("""
            { "638fcd23dc65553116701d33": {
                "id": "638fcd23dc65553116701d33",
                "name": "638fcd23dc65553116701d33 name",
                "objectives": [
                  { "id": "638fd070202cd55bee01ca11", "description": "638fd070202cd55bee01ca11", "bodyParts": ["Head", "Chest"] },
                  { "id": "6390a6fc9b4cbd1b5a9e7c1f", "description": "6390a6fc9b4cbd1b5a9e7c1f" }
                ] } }
            """)!;
        var de = new Dictionary<string, string> { ["638fcd23dc65553116701d33 name"] = "Prüfung", ["Head"] = "Kopf" };
        var en = new Dictionary<string, string>
        {
            ["638fcd23dc65553116701d33 name"] = "Audit",
            ["638fd070202cd55bee01ca11"] = "Locate and obtain the ledger",
            ["Chest"] = "Chest",
        };

        JsonTranslator.Translate(node, new HashSet<string> { "name", "description", "bodyParts" }, de, en);

        var task = node["638fcd23dc65553116701d33"]!;
        Assert.Equal("638fcd23dc65553116701d33", task["id"]!.GetValue<string>());
        Assert.Equal("Prüfung", task["name"]!.GetValue<string>());
        Assert.Equal("Locate and obtain the ledger", task["objectives"]![0]!["description"]!.GetValue<string>());
        Assert.Equal("Kopf", task["objectives"]![0]!["bodyParts"]![0]!.GetValue<string>());
        Assert.Equal("", task["objectives"]![1]!["description"]!.GetValue<string>()); // untranslated key → empty, not an id
    }

    [Fact]
    public void Reads_payload_and_translations_into_typed_records()
    {
        var dir = Directory.CreateTempSubdirectory("spotter-data-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(dir, "tasks.json"), """
                { "data": { "tasks": { "638fcd23dc65553116701d33": {
                    "id": "638fcd23dc65553116701d33", "name": "638fcd23dc65553116701d33 name", "trader": "5ac3b934156ae10c4430e83c",
                    "map": "5714dc692459777137212e12", "minPlayerLevel": 12, "kappaRequired": false, "lightkeeperRequired": false,
                    "taskRequirements": [ { "task": "5a68661a86f774500f48afb0", "status": ["complete"] } ],
                    "objectives": [ { "id": "638fd070202cd55bee01ca11", "description": "638fd070202cd55bee01ca11", "type": "findQuestItem",
                      "optional": false, "maps": ["5714dc692459777137212e12"], "count": 1, "questItem": "63a943cead5cc12f22161ff7",
                      "possibleLocations": [ { "map": "5714dc692459777137212e12", "positions": [ { "x": -177.12, "y": 6.15, "z": 227.13 } ] } ] } ] } } },
                  "translations": [ "$.data.tasks.*.name", "$.data.tasks.*.objectives[*].description" ] }
                """);
            File.WriteAllText(Path.Combine(dir, "tasks_en.json"), """
                { "data": { "638fcd23dc65553116701d33 name": "Audit", "638fd070202cd55bee01ca11": "Locate and obtain the ledger" } }
                """);
            var payload = new CachedResponse(Path.Combine(dir, "tasks.json"), false, false, DateTimeOffset.UtcNow);
            var english = new CachedResponse(Path.Combine(dir, "tasks_en.json"), false, false, DateTimeOffset.UtcNow);

            var tasks = GameDataLoader.Read(payload, english, english, "tasks", ApiJsonContext.Default.DictionaryStringApiTask);

            var audit = Assert.Single(tasks).Value;
            Assert.Equal("Audit", audit.Name);
            Assert.Equal(12, audit.MinPlayerLevel);
            Assert.Equal("5a68661a86f774500f48afb0", audit.TaskRequirements![0].Task);
            var objective = audit.Objectives![0];
            Assert.Equal("Locate and obtain the ledger", objective.Description);
            Assert.Equal(-177.12, objective.PossibleLocations![0].Positions![0].X);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
