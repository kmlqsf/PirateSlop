using PirateSlop.Networking;

namespace PirateSlop
{
    public static class UpgradeCardPresentation
    {
        public static int Rank(string rarity) => rarity switch { "Rare" => 1, "Epic" => 2, "Legendary" => 3, _ => 0 };
        public static string RarityName(string rarity) => rarity switch
        {
            "Rare" => "РЕДКОЕ", "Epic" => "ЭПИЧЕСКОЕ", "Legendary" => "ЛЕГЕНДАРНОЕ", _ => "ОБЫЧНОЕ"
        };

        public static string Category(string id) => id switch
        {
            "SeaLegs" or "DoubleJump" or "LightBoots" or "DeckAcrobat" or "WaterRun" or "RopeSprinter" => "ДВИЖЕНИЕ",
            "ToughPirate" or "LeadResolve" or "SecondWind" or "CharmedCoin" or "SeaPact" => "ЖИВУЧЕСТЬ",
            "SabreMaster" or "GhostSabre" => "САБЛЯ",
            "Bloodletter" or "DryPowder" or "QuickHands" or "Sharpshooter" or "Ricochet" or "ShotEcho" or "PushingBullets" => "ЛИЧНОЕ ОРУЖИЕ",
            "LongRangeCharge" or "GunnersEye" or "HeavyCannonball" or "SplitVolley" or "ExtraAmmoSlot" => "КАНОНИР",
            "FastCarpenterCommon" or "FastCarpenterRare" => "ПЛОТНИК",
            "HeartyCatch" or "LuckyBaitCommon" or "LuckyBaitRare" or "UnusualCatch" => "РЫБАЛКА",
            "FishSupply" or "Seeker" or "ExtraPocket" => "ДОБЫЧА",
            "Helmsman" or "SharpEye" or "RumSupply" or "ExtraMonkey" or "PirateFeast" => "ЭКИПАЖ",
            "LuckyCoin" or "LuckyDie" => "УДАЧА",
            _ => "УЛУЧШЕНИЕ"
        };

        public static InventoryItem IconItem(string id) => id switch
        {
            "SabreMaster" or "GhostSabre" or "Bloodletter" => InventoryItem.Sabre,
            "DryPowder" or "QuickHands" or "Ricochet" or "ShotEcho" or "PushingBullets" => InventoryItem.Pistol,
            "Sharpshooter" => InventoryItem.Musket,
            "LongRangeCharge" or "SplitVolley" or "ExtraAmmoSlot" => InventoryItem.Cannonball,
            "GunnersEye" or "HeavyCannonball" => InventoryItem.Cannon,
            "FastCarpenterCommon" or "FastCarpenterRare" => InventoryItem.Mallet,
            "LuckyBaitCommon" or "LuckyBaitRare" or "UnusualCatch" => InventoryItem.Rod,
            "HeartyCatch" or "FishSupply" or "PirateFeast" => InventoryItem.Fish,
            "Seeker" or "SharpEye" => InventoryItem.Spyglass,
            "RumSupply" => InventoryItem.Rum,
            "RopeSprinter" => InventoryItem.GrapplingHook,
            _ => InventoryItem.None
        };

        public static string Highlight(string id) => id switch
        {
            "SeaLegs" => "На скорости корабля",
            "DoubleJump" => "+1 прыжок в воздухе",
            "LightBoots" => "+30% к скорости бега",
            "DeckAcrobat" => "+50% к высоте прыжка",
            "ToughPirate" => "Здоровье ×2",
            "LeadResolve" => "−40% отбрасывания",
            "SecondWind" => "Восстановление здоровья",
            "CharmedCoin" => "Пережить смертельный удар",
            "SeaPact" => "Второй шанс",
            "SabreMaster" => "Три усиления сабли",
            "Bloodletter" => "Кровотечение от попаданий",
            "GhostSabre" => "Режущая волна",
            "DryPowder" => "+15% урона",
            "QuickHands" => "−20% времени зарядки",
            "Sharpshooter" => "+50% урона в голову",
            "Ricochet" => "Пуля найдёт второго врага",
            "ShotEcho" => "Один выстрел. Два попадания.",
            "LongRangeCharge" => "+30% скорости ядра",
            "GunnersEye" => "Видимая траектория",
            "HeavyCannonball" => "На 30% дольше сбиты с ног",
            "FastCarpenterCommon" => "+25% к ремонту",
            "FastCarpenterRare" => "+50% к ремонту",
            "Helmsman" => "+25% реакции штурвала",
            "HeartyCatch" => "+30% лечения рыбой",
            "LuckyBaitCommon" => "+25% к рыбалке",
            "LuckyBaitRare" => "+50% к рыбалке",
            "UnusualCatch" => "+50% шанса особого улова",
            "FishSupply" => "8 рыб в каждом сундуке",
            "PirateFeast" => "Лечение всему экипажу",
            "Seeker" => "Добыча на виду",
            "SharpEye" => "Метка на 15 секунд",
            "LuckyCoin" => "Два переброса",
            "SplitVolley" => "Два ядра за выстрел",
            "WaterRun" => "4 секунды по воде",
            "PushingBullets" => "Отбрасывание пулями",
            "ExtraPocket" => "+1 слот инвентаря",
            "ExtraAmmoSlot" => "+1 слот для ядер",
            "RopeSprinter" => "По канатам ×3 быстрее",
            "RumSupply" => "+1 бутылка рома",
            "LuckyDie" => "Одна шестёрка гарантирована",
            "ExtraMonkey" => "+1 обезьянка на корабле",
            _ => "Новая возможность"
        };

        public static string Description(UpgradeCard card) => card.id switch
        {
            "LeadResolve" => "Вражеские попадания отбрасывают вас на 40% слабее.",
            "SeaPact" => "Один раз за бой вы возрождаетесь через некоторое время на месте гибели с 50% здоровья.",
            "PushingBullets" => "Пули вашего личного оружия слегка отталкивают врагов назад.",
            "Seeker" => "Добыча видна по тем же признакам, что и через подзорную трубу.",
            "RumSupply" => "При выборе на полку вашего корабля добавляется одна бутылка рома.",
            "WaterRun" => "После схода с палубы или суши бегите по воде до 4 секунд. Восстановление: 10 секунд на твёрдой поверхности.",
            "ShotEcho" => "Первый выстрел после полной перезарядки: повтор через 0,3 с в том же направлении, 50% урона, без траты патрона.",
            "Bloodletter" => "Сабля и огнестрел: ещё 25% урона попадания за 4 с от кровотечения. Новое попадание обновляет эффект.",
            "SplitVolley" => "Каждый выстрел выпускает два ядра: под углом 10° влево и 10° вправо от направления прицеливания.",
            "UnusualCatch" => "Шанс дополнительного улова +50%: рыба-меч, рыба-фугу или случайный предмет.",
            "SecondWind" => "После 12 с без урона восстанавливайте по 2% максимального здоровья в секунду, до половины запаса.",
            "GhostSabre" => "Каждый четвёртый взмах выпускает режущую волну на 8 м. Стены и корпус блокируют волну.",
            _ => card.description
        };
    }
}
