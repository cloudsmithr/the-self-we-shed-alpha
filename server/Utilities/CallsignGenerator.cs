using System.Security.Cryptography;

namespace ApiServer.Utilities;

public class CallsignGenerator
{
        // 50 gloomy hues (no basic colors)
    private static readonly string[] Hues =
    {
        "Obsidian","Onyx","Eclipsed","Midnight","Nocturnal","Umbral","Tenebrous","Dusk","Twilight",
        "Nightshade","Raven","Charcoal","Ashen","Cinder","Slate","Smoke","Fog","Mist","Storm","Tempest",
        "Ominous","Void","Abyssal","Nebulous","Bruised","Veined","Rust","Sepia","Umber",
        "Ochre","Pallid","Placid","Osseous","Livid","Viridian","Miasmal","Blighted","Mildewed","Poison",
        "Acrid","Glimmer","Phosphor", "Sunless","Lightless","Starless","Overcast","Shrouded","Veiled",
        "Obscured","Penumbral", "Cadaverous","Gaunt","Withered","Bleak","Decrepit","Sepulchral",
        "Corroded","Tarnished","Charred","Scorched","Sinister","Malefic","Infernal",
        "Dire","Dread","Cursed","Forsaken","Wretched","Harrowed","Desolate","Fractured",
        "Shattered","Sunken","Gnarled","Jagged","Ruined","Scarred","Hollow","Sundered","Spectral",
        "Ethereal","Phantom","Elusive","Necrotic","Putrid","Fetid"
    };

    // 200 nouns (de-duped vs the above hue list)
    private static readonly string[] Nouns =
    {
        "Warden","Sentinel","Guardian","Watcher","Sentry","Ranger","Vanguard","Bulwark","Bastion","Paragon",
        "Champion","Protector","Custodian","Overseer","Marshal","Captain","Commander","Officer","Operator","Agent",
        "Handler","Specialist","Technician","Engineer","Analyst","Observer","Surveyor","Scout","Pathfinder","Navigator",
        "Courier","Runner","Strider","Drifter","Nomad","Pilgrim","Herald","Envoy","Emissary","Liaison",
        "Archivist","Curator","Librarian","Scribe","Chronicler","Recorder","Auditor","Inspector","Examiner","Seeker",
        "Finder","Listener","Whisper","Echo","Signal","Beacon","Lantern","Torch","Flare","Spark",
        "Ember","Ash","Haze","Shroud","Veil","Curtain","Screen","Mask","Mirror","Lens",
        "Prism","Spectrum","Cipher","Sigil","Glyph","Rune","Mark","Brand","Oracle","Augur",
        "Harbinger","Prophet","Medium","Channel","Conduit","Relay","Node","Anchor","Keystone","Pillar",
        "Spire","Tower","Citadel","Fortress","Redoubt","Rampart","Palisade","Barricade","Wall","Gate",
        "Portcullis","Lock","Key","Vault","Bunker","Shelter","Haven","Refuge","Sanctuary","Chapel",
        "Shrine","Reliquary","Relic","Totem","Talisman","Charm","Amulet","Grimoire","Codex","Manual",
        "Dossier","File","Archive","Ledger","Register","Index","Catalog","Map","Chart","Compass",
        "Sextant","Waypoint","Marker","Boundary","Threshold","Horizon","Summit","Ridge","Trench","Rift",
        "Breach","Fracture","Scar","Wound","Depth","Chasm","Maw","Fang","Talon",
        "Claw","Horn","Spine","Skull","Crown","Halo","Aegis","Shield","Helm","Gauntlet",
        "Blade","Spear","Pike","Lance","Arrow","Bolt","Hammer","Anvil","Forge","Crucible",
        "Furnace","Engine","Reactor","Siren","Alarm","Bell","Chime","Pulse","Frequency","Static",
        "Resonance","Tremor","Quake","Crossroad","Junction","Corridor","Passage","Causeway","Turnstile","Airlock",
        "Bulkhead","Canister","Capsule","Casket","Sarcophagus","Monolith","Obelisk","Labyrinth","Center","Outpost"
    };

    private const string Base36 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public static string Generate()
    {
        var hue = Hues[RandomNumberGenerator.GetInt32(Hues.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        var suffix = $"{Base36[RandomNumberGenerator.GetInt32(36)]}{Base36[RandomNumberGenerator.GetInt32(36)]}";
        return $"{hue}{noun}{suffix}";
    }

    /// <summary>
    /// Generates a callsign that is unique according to a caller-provided predicate.
    /// Example usage: await CallsignGenerator.GenerateUniqueAsync(name => !await db.Players.AnyAsync(p => p.DisplayName == name));
    /// </summary>
    public static async Task<string> GenerateUniqueAsync(Func<string, Task<bool>> isAvailable, int maxAttempts = 20)
    {
        for (var i = 0; i < maxAttempts; i++)
        {
            var candidate = Generate();
            if (await isAvailable(candidate))
                return candidate;
        }

        // Extremely unlikely fallback: add two more chars (Base36^4 = 1.6M suffixes)
        var baseName = $"{Hues[RandomNumberGenerator.GetInt32(Hues.Length)]} {Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)]}";
        var extra = $"{Base36[RandomNumberGenerator.GetInt32(36)]}{Base36[RandomNumberGenerator.GetInt32(36)]}" +
                    $"{Base36[RandomNumberGenerator.GetInt32(36)]}{Base36[RandomNumberGenerator.GetInt32(36)]}";
        return $"{baseName} {extra}";
    }
}