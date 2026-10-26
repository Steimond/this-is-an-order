# Баланс

Редактируйте .asset в Inspector: Faction1/Units, Faction1/Buildings и Shared/Economy. Параметры применяются при новом входе на карту. Resources/PrototypeBalanceCatalog хранит только ссылки на используемые ассеты.

Новые определения: Assets > Create > Order > Balance. Восстановить отсутствующие начальные ассеты: Order > Create Missing Balance Assets. Существующие числовые значения этот пункт не перезаписывает.

Скрипты ScriptableObject здесь отвечают за авторинг. При запуске они создают независимые данные Order.Content.MatchBalance для симуляции и интерфейса.
