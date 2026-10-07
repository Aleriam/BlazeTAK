namespace BlazeTAK.Client.Models;

public static class MarkerSymbolDefinitions
{
    public static List<MarkerCategory> GetCategories()
    {
        return new List<MarkerCategory>
        {
            new()
            {
                Id = "friendly_ground",
                Name = "Friendly Ground Units",
                Icon = "bi-shield-shaded",
                Symbols = new()
                {
                    new() { CategoryId = "friendly_ground", Name = "Infantry", Sidc = "SFGPUCI----", Description = "Friendly Infantry Unit" },
                    new() { CategoryId = "friendly_ground", Name = "Mechanized Infantry", Sidc = "SFGPUCIM---", Description = "Friendly Mechanized Infantry" },
                    new() { CategoryId = "friendly_ground", Name = "Armor / Tank", Sidc = "SFGPUCA----", Description = "Friendly Armored Unit" },
                    new() { CategoryId = "friendly_ground", Name = "Reconnaissance", Sidc = "SFGPUCR----", Description = "Friendly Recon Unit" },
                    new() { CategoryId = "friendly_ground", Name = "Field Artillery", Sidc = "SFGPUCD----", Description = "Friendly Artillery Battery" },
                    new() { CategoryId = "friendly_ground", Name = "Air Defense", Sidc = "SFGPUCDA---", Description = "Friendly Air Defense Unit" },
                    new() { CategoryId = "friendly_ground", Name = "Combat Engineers", Sidc = "SFGPUCE----", Description = "Friendly Engineers" },
                    new() { CategoryId = "friendly_ground", Name = "Command / HQ", Sidc = "SFGPUCH----", Description = "Friendly Headquarters" },
                    new() { CategoryId = "friendly_ground", Name = "Special Forces", Sidc = "SFGPUSF----", Description = "Friendly Special Operations" },
                    new() { CategoryId = "friendly_ground", Name = "Medical Unit", Sidc = "SFGPUSM----", Description = "Friendly Medical Team" },
                    new() { CategoryId = "friendly_ground", Name = "Supply / Logistics", Sidc = "SFGPUSS----", Description = "Friendly Supply Unit" },
                    new() { CategoryId = "friendly_ground", Name = "Sniper", Sidc = "SFGPUCIS---", Description = "Friendly Sniper Team" }
                }
            },
            new()
            {
                Id = "friendly_air",
                Name = "Friendly Aviation",
                Icon = "bi-airplane",
                Symbols = new()
                {
                    new() { CategoryId = "friendly_air", Name = "Attack Helicopter", Sidc = "SFAPMFRA---", Description = "Friendly Attack Helicopter" },
                    new() { CategoryId = "friendly_air", Name = "Utility / Transport Heli", Sidc = "SFAPMFRL---", Description = "Friendly Utility Helicopter" },
                    new() { CategoryId = "friendly_air", Name = "Fighter Aircraft", Sidc = "SFAPMFF----", Description = "Friendly Fighter Jet" },
                    new() { CategoryId = "friendly_air", Name = "UAV / Drone", Sidc = "SFAPMFQ----", Description = "Friendly Unmanned Aerial Vehicle" },
                    new() { CategoryId = "friendly_air", Name = "Medevac Helicopter", Sidc = "SFAPMFRM---", Description = "Friendly Medical Evacuation Heli" }
                }
            },
            new()
            {
                Id = "hostile_ground",
                Name = "Hostile Ground Units",
                Icon = "bi-exclamation-diamond",
                Symbols = new()
                {
                    new() { CategoryId = "hostile_ground", Name = "Hostile Infantry", Sidc = "SHGPUCI----", Description = "Enemy Infantry Unit" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile Mechanized", Sidc = "SHGPUCIM---", Description = "Enemy Mechanized Unit" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile Armor / Tank", Sidc = "SHGPUCA----", Description = "Enemy Armored Unit" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile Artillery", Sidc = "SHGPUCD----", Description = "Enemy Artillery Battery" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile Air Defense", Sidc = "SHGPUCDA---", Description = "Enemy Air Defense System" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile Recon", Sidc = "SHGPUCR----", Description = "Enemy Reconnaissance" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile HQ", Sidc = "SHGPUCH----", Description = "Enemy Headquarters" },
                    new() { CategoryId = "hostile_ground", Name = "Hostile Sniper", Sidc = "SHGPUCIS---", Description = "Enemy Sniper" }
                }
            },
            new()
            {
                Id = "hostile_air",
                Name = "Hostile Aviation",
                Icon = "bi-radioactive",
                Symbols = new()
                {
                    new() { CategoryId = "hostile_air", Name = "Hostile Fighter", Sidc = "SHAPMFF----", Description = "Enemy Fighter Aircraft" },
                    new() { CategoryId = "hostile_air", Name = "Hostile Attack Helicopter", Sidc = "SHAPMFRA---", Description = "Enemy Attack Helicopter" },
                    new() { CategoryId = "hostile_air", Name = "Hostile UAV / Drone", Sidc = "SHAPMFQ----", Description = "Enemy UAV / Drone" }
                }
            },
            new()
            {
                Id = "neutral_civilian",
                Name = "Neutral & Civilian",
                Icon = "bi-person",
                Symbols = new()
                {
                    new() { CategoryId = "neutral_civilian", Name = "Neutral Ground Unit", Sidc = "SNGPU------", Description = "Neutral Military / Police" },
                    new() { CategoryId = "neutral_civilian", Name = "Civilian Individual", Sidc = "SNGPE------", Description = "Civilian Person" },
                    new() { CategoryId = "neutral_civilian", Name = "Civilian Vehicle", Sidc = "SNGPEV-----", Description = "Civilian Vehicle / Transport" },
                    new() { CategoryId = "neutral_civilian", Name = "Neutral Medical / NGO", Sidc = "SNGPUSM----", Description = "Red Cross / Neutral Medical" }
                }
            },
            new()
            {
                Id = "tactical_points",
                Name = "Tactical Points & POIs",
                Icon = "bi-geo-alt",
                Symbols = new()
                {
                    new() { CategoryId = "tactical_points", Name = "Observation Post", Sidc = "SFGPO------", Description = "Observation / Listening Post" },
                    new() { CategoryId = "tactical_points", Name = "Checkpoint / TCP", Sidc = "SFGPECP----", Description = "Traffic Control Point / Checkpoint" },
                    new() { CategoryId = "tactical_points", Name = "Supply / Ammo Point", Sidc = "SFGPSPA----", Description = "Ammunition Supply Point" },
                    new() { CategoryId = "tactical_points", Name = "Medical Station", Sidc = "SFGPSPM----", Description = "Field Hospital / Medical Point" },
                    new() { CategoryId = "tactical_points", Name = "Obstacle / Minefield", Sidc = "SHGPENM----", Description = "Hazard / Minefield / Obstacle" },
                    new() { CategoryId = "tactical_points", Name = "Target / Point of Interest", Sidc = "SFGPE------", Description = "General Point of Interest" }
                }
            }
        };
    }
}
