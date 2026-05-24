# Generates src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json
# from minecraft_26.1.2_entity_textures.json using the same routing intent as CleanRoomEntityModelRuntime.TryBuildSpecific.
#
# Parity reference jars under tools/minecraft-parity/** must stay read-only. After editing this script's outputs,
# refresh javap columns using the checked-in class index (never mutate jars):
#   1) Regenerate index from a local read-only client.jar copy (ZipFile; jars are never modified):
#        python tools/build_minecraft_client_model_class_index.py <client.jar>
#   2) Apply post/pre-restructure FQCN fields:
#        python tools/sync_entity_manifest_deobf_from_jar.py
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$inv = Join-Path $repoRoot "src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_textures.json"
$out = Join-Path $repoRoot "src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json"

$j = Get-Content $inv -Raw | ConvertFrom-Json

function Get-Stem([string]$path) {
    [System.IO.Path]::GetFileNameWithoutExtension($path).ToLowerInvariant()
}

function Get-BuilderForPath([string]$path) {
    $norm = $path.Replace('\', '/').TrimStart('/')
    $stem = Get-Stem $path

    # Order mirrors CleanRoomEntityModelRuntime.TryBuildSpecific (specific before broad).
    if ($norm -match '/textures/entity/nautilus/') { return @{ builder = "NautilusMob"; deobf = "net.minecraft.client.model.animal.nautilus.NautilusModel" } }
    if ($stem -match 'horse_zombie|horse_skeleton') { return @{ builder = "Horse"; deobf = "net.minecraft.client.model.animal.equine.HorseModel" } }
    if ($norm -match '/textures/entity/zombie/' -and $norm -notmatch '/textures/entity/zombie_villager/') { return @{ builder = "Zombie"; deobf = "net.minecraft.client.model.ZombieModel" } }
    if ($norm -match '/textures/entity/zombie_villager/') { return @{ builder = "HumanoidZombieVillager"; deobf = "net.minecraft.client.model.ZombieVillagerModel" } }
    if ($stem -match 'zombie' -and $norm -notmatch '/textures/entity/zombie_villager/') { return @{ builder = "HumanoidZombie"; deobf = "net.minecraft.client.model.HumanoidModel" } }
    if ($norm -match '/textures/entity/wandering_trader/' -or $stem -match 'wandering_trader') { return @{ builder = "WanderingTrader"; deobf = "net.minecraft.client.model.VillagerModel" } }
    # Villager overlays (profession/type/level/baby variants) often have stems like "savanna" or "diamond",
    # so route by entity folder path instead of stem token matching.
    if ($norm -match '/textures/entity/villager/' -and $norm -notmatch '/textures/entity/zombie_villager/') { return @{ builder = "HumanoidVillager"; deobf = "net.minecraft.client.model.VillagerModel" } }
    if ($stem -match 'enderman') { return @{ builder = "Enderman"; deobf = "net.minecraft.client.model.EndermanModel" } }
    if ($stem -match 'witch') { return @{ builder = "Witch"; deobf = "net.minecraft.client.model.WitchModel" } }
    if ($stem -match 'evoker' -and $stem -notmatch 'fang') { return @{ builder = "Evoker"; deobf = "net.minecraft.client.model.IllagerModel" } }
    if ($stem -match 'vindicator') { return @{ builder = "Vindicator"; deobf = "net.minecraft.client.model.IllagerModel" } }
    if ($stem -match 'illusioner') { return @{ builder = "Illager"; deobf = "net.minecraft.client.model.IllagerModel" } }
    if ($stem -match 'pillager') { return @{ builder = "Pillager"; deobf = "net.minecraft.client.model.IllagerModel" } }
    if ($stem -match 'illager') { return @{ builder = "Illager"; deobf = "net.minecraft.client.model.IllagerModel" } }
    if ($stem -match 'cow|mooshroom') { return @{ builder = "Cow"; deobf = "net.minecraft.client.model.animal.cow.CowModel" } }
    if ($stem -match 'wolf') { return @{ builder = "Wolf"; deobf = "net.minecraft.client.model.WolfModel" } }
    if ($stem -match 'goat') { return @{ builder = "Goat"; deobf = "net.minecraft.client.model.GoatModel" } }
    if ($stem -match 'hoglin|zoglin') { return @{ builder = "Hoglin"; deobf = "net.minecraft.client.model.HoglinModel" } }
    if ($stem -match 'sniffer|snifflet' -or $norm -match '/textures/entity/sniffer/') { return @{ builder = "Sniffer"; deobf = "net.minecraft.client.model.animal.sniffer.SnifferModel" } }
    if ($stem -match 'wither') { return @{ builder = "Wither"; deobf = "net.minecraft.client.model.WitherBossModel" } }
    if ($stem -match 'warden') { return @{ builder = "Warden"; deobf = "net.minecraft.client.model.WardenModel" } }
    if ($stem -match 'magma_cube|magmacube') { return @{ builder = "MagmaCube"; deobf = "net.minecraft.client.model.LavaSlimeModel" } }
    if ($stem -match 'slime') { return @{ builder = "Slime"; deobf = "net.minecraft.client.model.SlimeModel" } }
    if ($stem -match 'silverfish') { return @{ builder = "Silverfish"; deobf = "net.minecraft.client.model.SilverfishModel" } }
    if ($stem -match 'endermite') { return @{ builder = "Endermite"; deobf = "net.minecraft.client.model.EndermiteModel" } }
    if ($stem -match 'shulker_bullet' -or $norm -match '/textures/entity/shulker/spark') { return @{ builder = "ShulkerBullet"; deobf = "net.minecraft.client.model.ShulkerBulletModel" } }
    if ($stem -match 'shulker') { return @{ builder = "Shulker"; deobf = "net.minecraft.client.model.ShulkerModel" } }
    if ($stem -match 'snow_golem|snowman') { return @{ builder = "SnowGolem"; deobf = "net.minecraft.client.model.SnowGolemModel" } }
    if ($stem -match 'iron_golem|irongolem') { return @{ builder = "IronGolem"; deobf = "net.minecraft.client.model.IronGolemModel" } }
    if ($stem -match 'end_crystal' -or $norm -match '/textures/entity/end_crystal/') { return @{ builder = "EndCrystal"; deobf = "net.minecraft.client.model.EndCrystalModel" } }
    if ($stem -match 'evoker_fangs' -or $norm -match '/textures/entity/illager/evoker_fangs') { return @{ builder = "EvokerFangs"; deobf = "net.minecraft.client.model.EvokerFangsModel" } }
    if ($stem -match 'spit') { return @{ builder = "LlamaSpit"; deobf = "net.minecraft.client.model.LlamaSpitModel" } }
    if ($stem -match 'arrow_spectral') { return @{ builder = "ArrowSpectral"; deobf = "net.minecraft.client.model.ArrowModel" } }
    if ($stem -match 'arrow_tipped') { return @{ builder = "ArrowTipped"; deobf = "net.minecraft.client.model.ArrowModel" } }
    if ($stem -match 'arrow') { return @{ builder = "Arrow"; deobf = "net.minecraft.client.model.ArrowModel" } }
    if ($stem -match 'wind_charge' -or $norm -match '/textures/entity/projectiles/wind_charge') { return @{ builder = "WindCharge"; deobf = "net.minecraft.client.model.WindChargeModel" } }
    if ($stem -match 'trident' -or $norm -match '/textures/entity/trident') { return @{ builder = "Trident"; deobf = "net.minecraft.client.model.TridentModel" } }
    if ($stem -match 'shield' -or $norm -match '/textures/entity/shield') { return @{ builder = "Shield"; deobf = "net.minecraft.client.model.ShieldModel" } }
    if ($stem -match 'banner' -or $norm -match '/textures/entity/banner/' -or $norm -match '/textures/entity/banner_base\.png$') {
        $wall = $norm -match '/textures/entity/banner_base' -or $norm -match '/textures/entity/banner/banner_base'
        if ($wall) { return @{ builder = "BannerFlagWall"; deobf = "net.minecraft.client.model.BannerFlagModel" } }
        return @{ builder = "BannerFlagStanding"; deobf = "net.minecraft.client.model.BannerFlagModel" }
    }
    if ($stem -match 'bed' -or $norm -match '/textures/entity/bed/') { return @{ builder = "Bed"; deobf = "net.minecraft.client.model.BedModel" } }
    if ($norm -match '/textures/entity/equipment/happy_ghast_body/' -or $stem -match 'happy_ghast_ropes' -or $norm -match '/textures/entity/ghast/happy_ghast_ropes') {
        return @{ builder = "HappyGhastHarness"; deobf = "net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel" }
    }
    if ($norm -match '/textures/entity/equipment/wings/') { return @{ builder = "EquipmentWings"; deobf = "net.minecraft.client.model.ElytraModel" } }
    if ($norm -match '/textures/entity/equipment/nautilus_body/') { return @{ builder = "EquipmentNautilusArmor"; deobf = "net.minecraft.client.model.animal.nautilus.NautilusArmorModel" } }
    if ($norm -match '/textures/entity/equipment/nautilus_saddle/') { return @{ builder = "EquipmentNautilusSaddle"; deobf = "net.minecraft.client.model.animal.nautilus.NautilusSaddleModel" } }
    if ($norm -match '/textures/entity/equipment/camel_saddle/' -or $norm -match '/textures/entity/equipment/camel_husk_saddle/') { return @{ builder = "EquipmentCamelSaddle"; deobf = "net.minecraft.client.model.CamelSaddleModel" } }
    if ($norm -match '/textures/entity/equipment/horse_saddle/' -or $norm -match '/textures/entity/equipment/donkey_saddle/' -or $norm -match '/textures/entity/equipment/mule_saddle/' -or $norm -match '/textures/entity/equipment/skeleton_horse_saddle/' -or $norm -match '/textures/entity/equipment/zombie_horse_saddle/' -or $norm -match '/textures/entity/equipment/pig_saddle/' -or $norm -match '/textures/entity/equipment/strider_saddle/') {
        return @{ builder = "EquipmentSaddle"; deobf = "net.minecraft.client.model.EquipmentModelRenderer" }
    }
    if ($norm -match '/textures/entity/equipment/horse_body/') { return @{ builder = "EquipmentHorseArmor"; deobf = "net.minecraft.client.model.EquipmentModelRenderer" } }
    if ($norm -match '/textures/entity/equipment/llama_body/') { return @{ builder = "EquipmentLlamaBody"; deobf = "net.minecraft.client.model.EquipmentModelRenderer" } }
    if ($norm -match '/textures/entity/equipment/wolf_body/') { return @{ builder = "EquipmentWolfBody"; deobf = "net.minecraft.client.model.EquipmentModelRenderer" } }
    if ($norm -match '/textures/entity/equipment/humanoid_leggings/') { return @{ builder = "EquipmentHumanoidLeggings"; deobf = "net.minecraft.client.model.HumanoidArmorModel" } }
    if ($norm -match '/textures/entity/equipment/humanoid_baby/') { return @{ builder = "EquipmentHumanoidBaby"; deobf = "net.minecraft.client.model.HumanoidArmorModel" } }
    if ($norm -match '/textures/entity/equipment/humanoid/') { return @{ builder = "EquipmentHumanoid"; deobf = "net.minecraft.client.model.HumanoidArmorModel" } }
    if ($norm -match '/textures/entity/equipment/' ) { return @{ builder = "EquipmentLayer"; deobf = "net.minecraft.client.model.EquipmentModelRenderer" } }
    if ($stem -match 'skull' -or $norm -match '/textures/entity/skull/') {
        if ($stem -match 'piglin') { return @{ builder = "PiglinSkull"; deobf = "net.minecraft.client.model.SkullModel" } }
        return @{ builder = "Skull"; deobf = "net.minecraft.client.model.SkullModel" }
    }
    if ($stem -match 'bell' -or $norm -match '/textures/entity/bell/') { return @{ builder = "Bell"; deobf = "net.minecraft.client.model.BellModel" } }
    if ($stem -match 'minecart') { return @{ builder = "Minecart"; deobf = "net.minecraft.client.model.MinecartModel" } }
    if ($norm -match '/textures/entity/chest_boat/') {
        return @{ builder = "ChestBoat"; deobf = "net.minecraft.client.model.BoatModel" }
    }
    if ($norm -match '/textures/entity/boat/') {
        return @{ builder = "Boat"; deobf = "net.minecraft.client.model.BoatModel" }
    }
    if ($stem -match 'leash_knot|lead_knot' -or $norm -match '/textures/entity/leash_knot|/textures/entity/lead_knot') { return @{ builder = "LeashKnot"; deobf = "net.minecraft.client.model.LeashKnotModel" } }
    if ($norm -match '/textures/entity/armorstand/') { return @{ builder = "ArmorStand"; deobf = "net.minecraft.client.model.ArmorStandModel" } }
    if ($stem -match 'ravager') { return @{ builder = "Ravager"; deobf = "net.minecraft.client.model.RavagerModel" } }
    if ($stem -match 'armadillo') { return @{ builder = "Armadillo"; deobf = "net.minecraft.client.model.ArmadilloModel" } }
    if ($norm -match '/textures/entity/breeze/' -or $stem -match 'breeze') { return @{ builder = "Breeze"; deobf = "net.minecraft.client.model.BreezeModel" } }
    if ($stem -match 'llama') { return @{ builder = "Llama"; deobf = "net.minecraft.client.model.LlamaModel" } }
    if ($stem -match 'camel') { return @{ builder = "Camel"; deobf = "net.minecraft.client.model.CamelModel" } }
    if ($stem -match 'panda') { return @{ builder = "Panda"; deobf = "net.minecraft.client.model.PandaModel" } }
    if ($stem -match 'polar_bear|polarbear') { return @{ builder = "PolarBear"; deobf = "net.minecraft.client.model.PolarBearModel" } }
    if ($stem -match 'zombified_piglin') { return @{ builder = "ZombifiedPiglin"; deobf = "net.minecraft.client.model.PiglinModel" } }
    if ($stem -match 'piglin') { return @{ builder = "Piglin"; deobf = "net.minecraft.client.model.PiglinModel" } }
    if ($stem -match 'pig_cold') { return @{ builder = "ColdPig"; deobf = "net.minecraft.client.model.animal.pig.ColdPigModel" } }
    if ($stem -match 'pig') { return @{ builder = "Pig"; deobf = "net.minecraft.client.model.PigModel" } }
    if ($stem -match 'sheep') { return @{ builder = "Sheep"; deobf = "net.minecraft.client.model.SheepModel" } }
    if ($stem -match 'donkey|mule') { return @{ builder = "DonkeyMuleHorse"; deobf = "net.minecraft.client.model.animal.equine.DonkeyModel" } }
    if ($stem -match 'horse') { return @{ builder = "Horse"; deobf = "net.minecraft.client.model.animal.equine.HorseModel" } }
    if ($stem -match 'rabbit' -or $norm -match '/textures/entity/rabbit/') { return @{ builder = "Rabbit"; deobf = "net.minecraft.client.model.RabbitModel" } }
    if ($stem -match 'dolphin') { return @{ builder = "Dolphin"; deobf = "net.minecraft.client.model.DolphinModel" } }
    if ($stem -match 'cat|ocelot') { return @{ builder = "Cat"; deobf = "net.minecraft.client.model.CatModel" } }
    if ($stem -match 'fox') { return @{ builder = "Fox"; deobf = "net.minecraft.client.model.animal.fox.FoxModel" } }
    if ($stem -match 'chicken') { return @{ builder = "Chicken"; deobf = "net.minecraft.client.model.ChickenModel" } }
    if ($stem -match 'creeper') { return @{ builder = "Creeper"; deobf = "net.minecraft.client.model.CreeperModel" } }
    if ($stem -match 'spider') { return @{ builder = "Spider"; deobf = "net.minecraft.client.model.SpiderModel" } }
    if ($stem -match 'dragon_fireball' -or $norm -match '/textures/entity/enderdragon/dragon_fireball') { return @{ builder = "DragonFireball"; deobf = "net.minecraft.client.model.DragonFireballModel" } }
    if ($stem -match 'enderdragon|dragon') { return @{ builder = "EnderDragon"; deobf = "net.minecraft.client.model.EnderDragonModel" } }
    if ($stem -match 'bat') { return @{ builder = "Bat"; deobf = "net.minecraft.client.model.BatModel" } }
    if ($stem -match 'blaze') { return @{ builder = "Blaze"; deobf = "net.minecraft.client.model.BlazeModel" } }
    if ($stem -eq 'bee_stinger' -or $norm -match '/textures/entity/bee/bee_stinger') { return @{ builder = "BeeStinger"; deobf = "net.minecraft.client.model.BeeStingerModel" } }
    if ($stem -match 'bee') { return @{ builder = "Bee"; deobf = "net.minecraft.client.model.BeeModel" } }
    if ($stem -match 'allay') { return @{ builder = "Allay"; deobf = "net.minecraft.client.model.AllayModel" } }
    if ($stem -match 'vex') { return @{ builder = "Vex"; deobf = "net.minecraft.client.model.VexModel" } }
    if ($stem -match 'phantom') { return @{ builder = "Phantom"; deobf = "net.minecraft.client.model.PhantomModel" } }
    if ($stem -match 'parrot') { return @{ builder = "Parrot"; deobf = "net.minecraft.client.model.ParrotModel" } }
    if ($stem -match 'zombified_piglin') { return @{ builder = "ZombifiedPiglin"; deobf = "net.minecraft.client.model.PiglinModel" } }
    if ($stem -match 'happy_ghast' -or $norm -match '/textures/entity/happy_ghast' -or $norm -match '/textures/entity/ghast/happy_ghast') {
        return @{ builder = "HappyGhast"; deobf = "net.minecraft.client.model.animal.ghast.HappyGhastModel" }
    }
    if ($stem -match 'ghast') { return @{ builder = "Ghast"; deobf = "net.minecraft.client.model.GhastModel" } }
    if ($stem -match 'guardian_elder|elder_guardian' -or $norm -match '/textures/entity/guardian_elder') { return @{ builder = "GuardianElder"; deobf = "net.minecraft.client.model.GuardianModel" } }
    if ($stem -match 'guardian_beam') { return @{ builder = "GuardianBeam"; deobf = "net.minecraft.client.renderer.entity.GuardianRenderer" } }
    if ($stem -match 'guardian') { return @{ builder = "Guardian"; deobf = "net.minecraft.client.model.GuardianModel" } }
    if ($stem -match 'pufferfish') { return @{ builder = "Pufferfish"; deobf = "net.minecraft.client.model.PufferfishSmallModel" } }
    if ($stem -match 'turtle') { return @{ builder = "Turtle"; deobf = "net.minecraft.client.model.TurtleModel" } }
    if ($stem -match 'glow_squid|squid') { return @{ builder = "Squid"; deobf = "net.minecraft.client.model.SquidModel" } }
    if ($stem -match 'salmon') { return @{ builder = "Salmon"; deobf = "net.minecraft.client.model.SalmonModel" } }
    if ($stem -match 'cod') { return @{ builder = "Cod"; deobf = "net.minecraft.client.model.CodModel" } }
    if ($stem -match 'tropical_fish_b' -or $norm -match '/textures/entity/fish/tropical_fish_b') { return @{ builder = "TropicalFishB"; deobf = "net.minecraft.client.model.TropicalFishModelB" } }
    if ($stem -match 'tropical_fish_a' -or $norm -match '/textures/entity/fish/tropical_fish_a' -or $stem -match 'tropical_fish') { return @{ builder = "TropicalFishA"; deobf = "net.minecraft.client.model.TropicalFishModelA" } }
    if ($norm -match '/textures/entity/strider/') { return @{ builder = "Strider"; deobf = "net.minecraft.client.model.StriderModel" } }
    if ($norm -match '/textures/entity/tadpole/') { return @{ builder = "Tadpole"; deobf = "net.minecraft.client.model.TadpoleModel" } }
    if ($norm -match '/textures/entity/axolotl/') { return @{ builder = "Axolotl"; deobf = "net.minecraft.client.model.AxolotlModel" } }
    if ($norm -match '/textures/entity/frog/') { return @{ builder = "Frog"; deobf = "net.minecraft.client.model.FrogModel" } }
    if ($norm -match '/textures/entity/signs/hanging/') { return @{ builder = "HangingSignEntity"; deobf = "net.minecraft.client.model.HangingSignModel" } }
    if ($norm -match '/textures/entity/signs/') { return @{ builder = "StandingSignEntity"; deobf = "net.minecraft.client.model.SignModel" } }
    if ($norm -match '/textures/entity/decorated_pot/') { return @{ builder = "DecoratedPotEntity"; deobf = "net.minecraft.client.renderer.blockentity.DecoratedPotRenderer" } }
    if ($norm -match '/textures/entity/conduit/') { return @{ builder = "ConduitEntity"; deobf = "net.minecraft.client.model.ConduitModel" } }
    if ($norm -match '/textures/entity/creaking/') { return @{ builder = "Creaking"; deobf = "net.minecraft.client.model.CreakingModel" } }
    if ($stem -match 'experience_orb' -or $norm -match '/textures/entity/experience_orb') { return @{ builder = "ExperienceOrb"; deobf = "net.minecraft.client.model.ExperienceOrbModel" } }
    if ($stem -match 'fishing_hook' -or $norm -match '/textures/entity/fishing_hook') { return @{ builder = "FishingHook"; deobf = "net.minecraft.client.model.FishingHookModel" } }
    if ($stem -match 'beacon_beam' -or $norm -match '/textures/entity/beacon_beam') { return @{ builder = "BeaconBeam"; deobf = "net.minecraft.client.model.BeaconBeamModel" } }
    if ($norm -match '/textures/entity/zombie_villager/') { return @{ builder = "HumanoidZombieVillager"; deobf = "net.minecraft.client.model.HumanoidModel" } }
    if ($norm -match '/textures/entity/villager/') { return @{ builder = "HumanoidVillager"; deobf = "net.minecraft.client.model.HumanoidModel" } }
    if ($stem -match 'giant') { return @{ builder = "HumanoidGiant"; deobf = "net.minecraft.client.model.HumanoidModel" } }
    if ($norm -match '/textures/entity/fish/') {
        if ($stem -match 'tropical_b') { return @{ builder = "TropicalFishB"; deobf = "net.minecraft.client.model.TropicalFishModelB" } }
        if ($stem -match 'tropical_a' -or $stem -match 'tropical') { return @{ builder = "TropicalFishA"; deobf = "net.minecraft.client.model.TropicalFishModelA" } }
        if ($stem -match 'puffer') { return @{ builder = "Pufferfish"; deobf = "net.minecraft.client.model.PufferfishSmallModel" } }
        if ($stem -match 'salmon') { return @{ builder = "Salmon"; deobf = "net.minecraft.client.model.SalmonModel" } }
        if ($stem -match 'cod') { return @{ builder = "Cod"; deobf = "net.minecraft.client.model.CodModel" } }
    }
    if ($stem -match 'end_gateway_beam' -or $norm -match '/textures/entity/end_gateway_beam') { return @{ builder = "BeamColumn"; deobf = "net.minecraft.client.model.EndGatewayBeamModel" } }
    if ($norm -match '/textures/entity/skeleton/') { return @{ builder = "Skeleton"; deobf = "net.minecraft.client.model.SkeletonModel" } }
    if ($stem -match 'end_portal' -or $norm -match '/textures/entity/end_portal') { return @{ builder = "EndPortalSurface"; deobf = "net.minecraft.client.model.EndPortalModel" } }
    if ($norm -match '/textures/entity/cat/') { return @{ builder = "Cat"; deobf = "net.minecraft.client.model.CatModel" } }
    if ($stem -match 'enchanting_table_book' -or $norm -match '/textures/entity/enchanting_table_book') { return @{ builder = "EnchantingTableBook"; deobf = "net.minecraft.client.model.BookModel" } }
    if ($norm -match '/textures/entity/copper_golem/') { return @{ builder = "CopperGolem"; deobf = "net.minecraft.client.model.animal.golem.CopperGolemModel" } }
    if ($norm -match '/textures/entity/chest/') { return @{ builder = "ChestEntity"; deobf = "net.minecraft.client.model.ChestModel" } }
    if ($norm -match '/textures/entity/player/slim/') { return @{ builder = "PlayerSlim"; deobf = "net.minecraft.client.model.PlayerModel" } }
    if ($norm -match '/textures/entity/player/wide/') { return @{ builder = "PlayerWide"; deobf = "net.minecraft.client.model.PlayerModel" } }
    if ($norm -match '/textures/entity/player/') { return @{ builder = "PlayerHumanoid"; deobf = "net.minecraft.client.model.PlayerModel" } }
    if ($norm -match '/textures/entity/llama/') { return @{ builder = "Llama"; deobf = "net.minecraft.client.model.LlamaModel" } }

    # Broad fallbacks last (family keys) — inventory should not rely on these if JSON is complete.
    if ($stem -match 'zombie|skeleton|stray|husk|drowned|player|steve|alex|villager|witch|pillager|illager|vindicator|evoker|wandering_trader|enderman|piglin|zombified_piglin') {
        return @{ builder = "HumanoidGeneric"; deobf = "net.minecraft.client.model.HumanoidModel" }
    }
    if ($stem -match 'cow|mooshroom|pig|sheep|wolf|fox|ocelot|cat|horse|donkey|mule|camel|goat|llama|trader_llama|panda|polar_bear|rabbit') {
        return @{ builder = "QuadrupedFamily"; deobf = "net.minecraft.client.model.QuadrupedModel" }
    }
    if ($stem -match 'bat|bee|parrot|phantom|vex|ghast|blaze|allay') { return @{ builder = "FlyingFamily"; deobf = "net.minecraft.client.model.HierarchicalModel" } }
    if ($stem -match 'salmon|cod|pufferfish|tropical_fish|squid|glow_squid|dolphin|guardian|turtle') { return @{ builder = "AquaticFamily"; deobf = "net.minecraft.client.model.HierarchicalModel" } }

    return @{ builder = "Unknown"; deobf = "" }
}

function Get-GeometryIrOfficialJvm([string]$path, [string]$builder) {
    $norm = $path.Replace('\', '/')
    if ($norm -match '/textures/entity/equipment/wings/') { return "net.minecraft.client.model.object.equipment.ElytraModel" }
    if ($norm -match '/textures/entity/equipment/humanoid_leggings/') { return "net.minecraft.client.model.EquipmentHumanoidLeggingsModel" }
    if ($norm -match '/textures/entity/equipment/humanoid_baby/' -or $norm -match '/textures/entity/equipment/humanoid/') { return "net.minecraft.client.model.HumanoidModel" }
    if ($norm -match '/textures/entity/equipment/horse_body/' -or $norm -match '/textures/entity/equipment/skeleton_horse_body/' -or $norm -match '/textures/entity/equipment/zombie_horse_body/') { return "net.minecraft.client.model.animal.equine.HorseModel" }
    if ($norm -match '/textures/entity/equipment/(donkey|mule)_body/') { return "net.minecraft.client.model.animal.equine.DonkeyModel" }
    if ($norm -match '/textures/entity/equipment/llama_body/') { return "net.minecraft.client.model.animal.llama.LlamaModel" }
    if ($norm -match '/textures/entity/equipment/wolf_body/') { return "net.minecraft.client.model.animal.wolf.WolfModel" }
    if ($norm -match '/textures/entity/equipment/strider_saddle/') { return "net.minecraft.client.model.monster.strider.StriderModel" }
    if ($norm -match '/textures/entity/equipment/nautilus_body/') { return "net.minecraft.client.model.animal.nautilus.NautilusArmorModel" }
    if ($norm -match '/textures/entity/equipment/nautilus_saddle/') { return "net.minecraft.client.model.animal.nautilus.NautilusSaddleModel" }
    if ($norm -match '/textures/entity/equipment/camel_saddle/' -or $norm -match '/textures/entity/equipment/camel_husk_saddle/') { return "net.minecraft.client.model.animal.camel.CamelSaddleModel" }
    if ($norm -match '/textures/entity/equipment/(horse|donkey|mule|skeleton_horse|zombie_horse)_saddle/') { return "net.minecraft.client.model.animal.equine.EquineSaddleModel" }
    if ($builder -eq "Bed") { return "net.minecraft.client.model.BedModel" }
    if ($builder -eq "StandingSignEntity") { return "net.minecraft.client.model.SignModel" }
    if ($builder -eq "HangingSignEntity") { return "net.minecraft.client.model.HangingSignModel" }
    if ($builder -eq "DecoratedPotEntity") { return "net.minecraft.client.model.DecoratedPotModel" }
    if ($builder -eq "ConduitEntity") { return "net.minecraft.client.model.ConduitModel" }
    if ($builder -eq "BeaconBeam") { return "net.minecraft.client.model.BeaconBeamModel" }
    return $null
}

$rules = @()
foreach ($f in $j.files) {
    if ($f.path -notlike "*.png") { continue }
    $p = $f.path.Replace('\', '/')
    $prefix = $p.Substring(0, $p.Length - 4)
    $info = Get-BuilderForPath $p
    if ($info.builder -eq "Unknown") {
        Write-Warning "Unknown builder for $p"
    }
    $row = [ordered]@{
        path_prefix = $prefix
        builder_method = $info.builder
        deobf_model_class = $info.deobf
        notes = ""
    }
    $irJvm = Get-GeometryIrOfficialJvm $p $info.builder
    if ($irJvm) { $row.geometry_ir_official_jvm = $irJvm }
    $rules += $row
}

$rules = $rules | Sort-Object { -$_.path_prefix.Length }

$doc = [ordered]@{
    source_minecraft_version = "26.1.2"
    rules = $rules
}

$json = $doc | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($out, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Wrote $($rules.Count) rules to $out"
Write-Host "Run: python tools/sync_entity_manifest_deobf_from_jar.py (after refreshing model class index if needed)."
