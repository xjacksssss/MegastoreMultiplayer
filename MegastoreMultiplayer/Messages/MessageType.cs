namespace MegastoreMultiplayer.Messages
{
    public enum MessageType : byte
    {
        // Bidirectional game-state messages (any sender → host → all others)
        PlayerPosition   = 1,
        ShelfUpdate      = 2,
        MoneyUpdate      = 3,
        OrderConfirmed   = 4,
        BoxPicked        = 5,
        BoxDropped       = 6,
        OvenStart        = 9,
        OvenComplete     = 10,
        TutorialStep     = 11,
        ObjectiveStep    = 12,
        PriceUpdate      = 13,
        PlaceableMoved   = 14,
        FurnitureMoved   = 15,
        StoreOpenClose   = 16,  // OpenCloseLabel toggled
        LicensePurchased = 17,  // ProductLicenseManager.PurchaseLicense
        StoreGrowth      = 18,  // GrowthManager.PurchaseGrowth
        DayEnded         = 19,  // TimeManager.OnEndDay
        NewDayStarted    = 20,  // TimeManager.StartTheNewDay
        TrayPickedUp     = 21,  // TrayManager.PickUpTray
        TrayPutDown      = 22,  // Tray placed back
        LevelUp          = 23,  // ExperienceManager.LevelUp
        LightToggle      = 24,  // LightSwitch.OnMouseButtonDown
        EmployeeHired    = 25,  // EmployeeManager.HireEmployee
        EmployeeFired    = 26,  // EmployeeManager.FireEmployee
        BoxThrown        = 27,  // box thrown — position + velocity for immediate appearance
        VehiclePosition  = 28,  // vehicle driven by a player — unreliable at 20 Hz
        BoxOnVehicle     = 29,  // box loaded onto a hand truck / vehicle
        VehicleUnloaded  = 30,  // all boxes unloaded from a vehicle
        NpcPosition       = 31, // customer NPC position (networkId, pos, euler) from host
        NpcSpawn              = 45, // host activated a customer — client shows real model
        NpcDespawn            = 46, // host deactivated a customer — client hides it
        EmployeeActivated     = 47, // host activated an employee NPC
        EmployeeDeactivated   = 48, // host deactivated an employee NPC
        EmployeePosition      = 49, // employee position update (10 Hz, unreliable)
        CarSpawn              = 50, // customer car entered parking lot
        CarPosition           = 51, // car transform update (10 Hz, unreliable)
        CarDespawn            = 52, // customer car left parking lot
        TruckArrived          = 53, // delivery truck docked at a receiving area
        TruckLeft             = 54, // delivery truck departed
        EmployeeUpdated       = 55, // employee assignment/stats changed mid-session
        StoreName             = 56, // store name or sign style changed
        CustomerAnim          = 57, // customer NPC animation trigger (pickup, idle, etc.)
        CheckoutStep          = 58, // checkout desk conveyor/scan step
        WandererSpawn         = 59, // ambient street wanderer activated by host
        WandererDespawn       = 60, // ambient street wanderer returned to pool
        AdUpdate          = 32, // OfferManager activated or deactivated
        RecyclingDone     = 34, // box deleted via Trash
        BakeryDoughLoaded = 35, // tray with dough slotted into an oven
        PlayerName        = 37, // peer announces their display name
        MigrationPlan     = 38, // host tells clients who to reconnect to if host drops
        PalletPickedUp    = 40, // pallet lifted by vehicle or player
        PalletDropped     = 41, // pallet placed / thrown / released by vehicle
        PalletTrashed     = 42, // pallet destroyed via trash
        SyncCheck         = 43, // host broadcasts state hash; clients compare
        ResyncRequest     = 44, // client asks host to re-send full StateSnapshot
        XpUpdate          = 61, // current XP value changed (per-sale delta broadcast)
        VendingMoneyUpdate  = 62, // VendingPlaceable storedMoney changed (purchase or collection)
        DecorationUsed      = 63, // floor or wall decoration changed mid-session
        ChoppingStandSit    = 64, // a player sat down at a chopping stand
        ChoppingStandLeave  = 65, // a player left a chopping stand
        ChoppingAskProduct  = 66, // NPC ordered at chopping stand — broadcast products to clients
        ChoppingProductGiven = 67, // player gave prepared product; client->host relay to complete NPC tx
        BoxOpened        = 75, // player opened/cut a box lid — sync visual open state
        EmployeeAnim     = 76, // employee animation trigger (work, idle) broadcast by host

        // Cash register / checkout desk
        CheckoutCustomerQueued  = 68, // customer NPC joined checkout queue (host → all)
        CheckoutProductsPlaced  = 69, // customer placed products on belt (host → all), includes price per product
        CheckoutCustomerLeft    = 70, // customer dequeued after paying (host → all)
        CheckoutManualScan      = 71, // player manually scanned a product (any → relay) — NOT automated cashier
        CheckoutPaymentCash     = 72, // customer offered cash (host → all)
        CheckoutPaymentCard     = 73, // customer offered card (host → all)
        CheckoutPaymentTaken    = 74, // player took payment (client → host to complete NPC tx)

        // Host → new client only
        StateSnapshot    = 7,
        SaveDataSync     = 8,
    }
}
