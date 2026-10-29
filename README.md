# This Is an Order

Голосовая RTS на Unity: люди и нанитные машины, экономика, базы и бой. Сначала бот, затем PvP 1 на 1; целевая платформа — Windows / Steam.

**[Открыть весь проект в одном HTML-документе](docs/PROJECT.html)** — выбранные концепты со встроенными изображениями, полный текст правил и таблиц, поиск, оглавление и печать / PDF. Работает без интернета.

[Исходный дизайн-документ](docs/DESIGN.md) — единственный актуальный текстовый источник. После изменений пересобрать HTML: `node tools/docs/build-handbook.cjs` (использует пакеты `marked` и `sharp` из комплектного Node.js Codex).

- [Выбранные концепты в общем документе](docs/PROJECT.html#visuals).
- [Резонанс: единые основные визуалы и модельные листы](docs/concepts/resonance-final/gallery.html) — 10 юнитов в утверждённом стиле чистых панелей.
- [Транспортник Резонанса: выбран исходный G](docs/concepts/resonance-transport-reboot/gallery.html#g) — закрытая нижняя капсула; [ракурсы для моделирования и OBJ](docs/concepts/resonance-transport-reboot/modeling/gallery.html).
- [Резонанс: ракурсы всех десяти зданий](docs/concepts/resonance-buildings-modeling/gallery.html) — основные виды, семь новых модельных листов, каркасы и OBJ.
- [Здания: второй проход и выбор 28 октября](docs/concepts/resonance-buildings-review/round-2/gallery.html) — Вычислитель и Узел прошивки без баков, Гнездо A2 / F.
- [42 новых концепта зданий Резонанса](docs/concepts/resonance-buildings-review/gallery.html) — по шесть вариантов для семи зданий; [проверка выбранных построек](docs/concepts/resonance-buildings-review/audit/gallery.html).
- [Что осталось перенести и уточнить](docs/DESIGN.md#audit).
- [Единый реестр балансных вопросов перед релизом](docs/DESIGN.md#balance-register).
- [Следующие задачи](docs/DESIGN.md#backlog).
- [Правила подготовки моделей в Blender](docs/BLENDER_ASSET_RULES.md) — обязательная проверка анимации работы, зон цвета игрока, разделения частей и внутренних поверхностей.
- [Архив документов и скетчей](docs/Old/2026-10-25/README-ARCHIVE.md).

## Запуск

Открывать в Unity Hub подпапку `Game/`. Зафиксированная версия проекта — Unity 6000.6.6f1, URP 17.6.0. Подготовка локальной текстовой модели: `./tools/Prepare-LocalAI.ps1`. Сцены: `MainMenu.unity`, `TempleBasin.unity`, `VisualLab.unity` в `Game/Assets/Order/Scenes`.

По последним записям полигон уже содержит экономику, бой и простого бота. Новый полный баланс и графика ещё не внедрены; микрофон/STT, TTS, PvP и масштаб массового боя не подтверждены. Текущий голосовой путь пока проверяется текстом. [Подробный статус](docs/DESIGN.md#prototype), [локальная модель](docs/DESIGN.md#local-ai), [проверки](docs/DESIGN.md#verification).

Исторические версии документов сохранены. Старые имена страниц в docs оставлены как переходы; актуальные игровые правила и дизайн редактируются в DESIGN.md. Производственные инструкции для моделей ведутся в BLENDER_ASSET_RULES.md и связаны с разделом 18 дизайн-документа.
