/* Rebuild docs/PROJECT.html from DESIGN.md. Uses bundled marked and sharp.
   Run: node tools/docs/build-handbook.cjs */
const fs = require('node:fs');
const path = require('node:path');
const { createRequire } = require('node:module');
const root = path.resolve(__dirname, '../..');
const runtimeRequire = createRequire(path.join(path.dirname(process.execPath), '../node_modules/package.json'));
const sharp = runtimeRequire('sharp');
const esc = s => String(s).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('"', '&quot;');
const legacyCards = [
  ['Нод · A1', 'Принято', 'Сферический резервуар, пояс качения, выдвижные золотистые электроды для контактной атаки. Добыча и строительство — нанитной массой. На общем листе выбран только A1.', 'worker-contact-directions/01-variants.png'],
  ['Паучок · B', 'Принято', 'Шесть ног, передняя ударная пара, группа сенсоров. Нейтральная стойка; удар показан отдельно.', 'spider-directions/03-b-model-sheet.png', 'spider-directions/02-b-refined.png'],
  ['Игломёт · C', 'Принято', 'Трёхствольная кассета; физические иглы, поочерёдная стрельба 1 → 2 → 3 с равными интервалами. На общем листе выбран C. Быстрые очереди — предложение исследования.', 'needler-directions/01-variants.png'],
  ['Излучатель · D', 'Принято', 'Гусеницы, открытая рама, одна центральная линза. Заднюю часть между видами ещё нужно согласовать.', 'emitter-directions/03-d-model-sheet.png'],
  ['Взломщик · A', 'Принято', 'Четыре колеса, широкая двухсекционная антенная панель. Оси наведения и части предстоит описать для производства.', 'saboteur-directions/02-a-model-sheet.png'],
  ['Голем · B5', 'Принято', 'Четыре гусеничных блока, перенос складной задней плиты вперёд. Две формы. Размещение: голограмма и протягивание для направления.', 'golem-mobile-directions/07-b5-transport-model-sheet.png', 'golem-mobile-directions/08-b5-deployed-model-sheet.png'],
  ['Ловец · B', 'Принято', 'Прямоугольные ракетные блоки частично утоплены под крылом без подвесов. Проекции проверить на едином 3D-блокинге.', 'catcher-airframe-directions/07-b-missile-refined.png', 'catcher-airframe-directions/08-b-model-reference.png'],
  ['Сеятель · A', 'Принято', 'Корпус A; новая роль — капсулы с контактными шариками, лечебный режим после исследования. Старое сервисное поле отменено. Механизм запуска и посадка для стрельбы ещё не утверждены.', 'seeder-directions/07-a-body-model-sheet.png'],
  ['Носитель нанитных гранат · D', 'Принято', 'Две гусеницы, центральный канал выпуска, три секции магазина. Старт с одной гранатой, максимум три; пополнение платное. Имя и ряд правил боя ещё открыты.', 'grenade-carrier-directions/07-d-model-sheet.png'],
  ['Ядро · A «Сердце»', 'Принято', 'Актуальный командный центр: физическая сфера в прозрачной камере, четыре опоры. Прежний A «Цитадель» заменён. Без ворот и фиксированного выхода.', 'core-heart-directions/07-a-model-sheet.png', 'core-heart-directions/01-a.png'],
  ['Сборочный узел · A «Цистерны»', 'Принято', 'Две полупрозрачные цистерны с нанитной массой, графитовый каркас. Прежний открытый тоннель отменён.', 'assembly-materials/07-a-model-sheet.png'],
  ['Страж · A + F', 'Принято', 'Основание и ПВО A, наземное орудие F. Одно здание, два взаимоисключающих режима. Общее основание свести в 3D; листы не являются ортографическими чертежами.', 'guardian-directions/08-af-ground-model-sheet.png', 'guardian-directions/09-af-air-model-sheet.png'],
  ['Люди · наземные силы', 'Направление', 'Инженерная экспедиция: оливково-серые и песочные корпуса, сталь, янтарные индикаторы. Рабочий — беспилотный четырёхколёсный дрон. Старый энергоклинок штурмовика заменён щитом и ружьём по правилам.', 'humans/01-human-ground.png'],
  ['Люди · авиация', 'Направление', 'Выбранное визуальное направление сохраняется. Точные роли и характеристики — в разделе 03.', 'humans/02-human-air.png'],
  ['Люди · здания', 'Направление', 'Сохраняем направление. Концепты не являются готовыми игровыми ассетами.', 'humans/05-human-buildings.png'],
  ['Люди · улучшения поддержки', 'Направление', 'Принцип визуальных модификаций; базовые модели брать с ростера.', 'humans/08-human-upgrades-support.png'],
  ['HUD · люди', 'Направление', 'Выразительный каркас, уменьшенный верх без названий ресурсов и карты. Новые изображения ещё не внедрены.', 'ui/01-human-hud.png'],
  ['HUD · наниты', 'Направление', 'Выбран каркас. Сцена, портреты и формы на этом листе устарели; их нужно обновить под актуальные концепты.', 'ui/02-nanite-hud.png'],
  ['Иконки · ростер людей', 'Направление', 'Проверить читаемость на малых размерах перед внедрением.', 'ui/03-human-roster-icons.png'],
  ['Иконки · способности и улучшения', 'Направление', 'Визуальные метафоры; механика и роли определяются правилами.', 'ui/06-human-abilities-upgrades.png'],
  ['Иконки · общие команды', 'Направление', 'Смысловые референсы. Переработку с человеком исправить; доступность команд зависит от расы.', 'ui/05-command-icons.png'],
  ['Ассистент · образы и состояния', 'Направление', 'Фракционные образы сохраняются. Слушание — проект состояния, не подтверждение готового голосового пути.', 'ui/09-assistant-and-feedback.png'],
  ['Планёр · доводка A', 'Открыто', 'Характер A подходит автору, но форма крыла в разных видах расходится. Финальный силуэт и модельный лист ещё не согласованы. Нижний TOP исходного листа не использовать для моделирования.', 'glider-directions/01-a.png'],
  ['Сеятель · система капсул', 'Предложение', 'Верхняя крышка, выходы, транспортная капсула, боевые и лечебные шарики — визуальное предложение. Посадка для стрельбы и число шариков не утверждены.', 'seeder-directions/09-a-capsule-system.png'],
  ['Игломёт · новые материалы', 'Эксперимент', 'Свежесобранный вариант без износа. Только проба материалов в стиле нового Ядра; утверждённый Игломёт C не заменяет.', 'needler-style-experiment/02-c-factory-new.png'],
];

const units = JSON.parse(fs.readFileSync(path.join(root, 'docs/concepts/resonance-final/manifest.json'), 'utf8'));
const oldUnitPrefixes = ['Нод ·', 'Паучок ·', 'Игломёт ·', 'Излучатель ·', 'Взломщик ·', 'Голем ·', 'Ловец ·', 'Сеятель ·', 'Носитель нанитных гранат ·', 'Планёр ·'];
const cards = [
  ...units.map(unit => [unit.label, 'Принято', unit.note + ' Ревизия v2. Художественные виды сверяются с контрольным каркасом; детальная геометрия генераций ещё не является точным чертежом.', ...[unit.hero, ...unit.sheets, ...(unit.guides || []), ...(unit.motion ? [unit.motion] : [])].map(file => 'resonance-final/' + file)]),
  ...[["Ретранслятор · A","Принято","Выбрана раздвоенная мачта A. Продлевает сеть, без лимита армии и производства.","resonance-buildings-review/relay/01-a.png"],["Матрица форм · E","Принято","Выбраны геометрические образцы под общим навесом. Прямоугольное пятно допустимо, точный размер уточняется.","resonance-buildings-review/matrix/05-e.png"],["Глубинное ядро · A","Принято","Выбран квадратный заглублённый колодец. Технология III; не источник сети.","resonance-buildings-review/deep-core/01-a.png"],["Резервуар · E","Принято","Выбрана наклонная одиночная ёмкость. Запас заряда для восстановления вне боя.","resonance-buildings-review/reservoir/05-e.png"]],
  ...[["Вычислитель · B5","Принято","Три твёрдые вычислительные кассеты под общей перемычкой, без нанитных баков. Выбрано автором.","resonance-buildings-review/round-2/computer-b5.png"],["Узел прошивки · B","Принято","Программирующий барабан и считывающая головка. Работа исследования будет читаться в анимации; анимация ещё не подготовлена.","resonance-buildings-review/round-2/firmware-b.png"],["Гнездо · A2","Принято","Три симметричных когтя вверх и к центру. Авиационные технологии; не производство.","resonance-buildings-review/round-2/nest-a2.png"]],
  ['Экстрактор плазмы · A', 'Принято', 'Выбран низкий кольцевой коллектор A: четыре крупные секции и четыре радиальных преобразователя вокруг центрального заборника. Автоматическая добыча без рабочих, только при действующей сети.', 'resonance-plasma-extractor-directions/a.png', 'resonance-buildings-modeling/plasma-extractor/model-sheet.png'],
  ['Транспортник Резонанса · G', 'Принято', 'Выбран исходный G: вертикальный носитель с полностью закрытой съёмной капсулой. Вариации G1–G6 — история. Подготовлены художественные виды с капсулой и без неё; геометрические каркасы и OBJ доступны в отдельном пакете. Параметры и анимации ещё открыты.', 'resonance-transport-reboot/g.png', 'resonance-transport-reboot/modeling/g-attached-model-sheet.png', 'resonance-transport-reboot/modeling/g-empty-model-sheet.png'],
  ...legacyCards.filter(card => !oldUnitPrefixes.some(prefix => card[0].startsWith(prefix))),
];

const buildingModels = JSON.parse(fs.readFileSync(path.join(root, 'docs/concepts/resonance-buildings-modeling/manifest.json'), 'utf8'));
for (const card of cards) {
  const model = buildingModels.jobs.find(j => j.label === card[0]);
  if (model) { card.splice(3, card.length - 3, model.hero, 'resonance-buildings-modeling/' + model.file); card[2] += ' Подготовлены модельные ракурсы; контрольные проекции и OBJ доступны в пакете зданий.'; }
  if (card[0] === 'Ядро · A «Сердце»') card.splice(3, card.length - 3, 'core-heart-directions/01-a.png', 'core-heart-directions/07-a-model-sheet.png');
  if (card[0] === 'Сборочный узел · A «Цистерны»') card.splice(3, card.length - 3, 'assembly-materials/01-a.png', 'assembly-materials/07-a-model-sheet.png');
}

async function main() {
  const { marked } = await import(require('node:url').pathToFileURL(runtimeRequire.resolve('marked')).href);
  const images = new Map();
  for (const card of cards) for (const file of card.slice(3)) {
    if (images.has(file)) continue;
    const buffer = await sharp(path.join(root, 'docs/concepts', file)).resize({ width: 1800, height: 1800, fit: 'inside', withoutEnlargement: true }).webp({ quality: 88 }).toBuffer();
    images.set(file, `data:image/webp;base64,${buffer.toString('base64')}`);
  }
  const md = fs.readFileSync(path.join(root, 'docs/DESIGN.md'), 'utf8');
  let body = marked.parse(md, { gfm: true });
  // Make every table scroll horizontally without shrinking the text.
  body = body.replaceAll('<table>', '<div class="table-wrap"><table>').replaceAll('</table>', '</table></div>');
  let heading = 0;
  body = body.replace(/<h([23])>([\s\S]*?)<\/h\1>/g, (_, level, text) => `<h${level} id="heading-${++heading}">${text}</h${level}>`);
  const anchors = [...md.matchAll(/<a id="([^"]+)"><\/a>\s+## ([^\n]+)/g)].map(m => [m[1], m[2].trim()]);
  const toc = anchors.map(([id, title]) => `<a href="#${id}">${esc(title)}</a>`).join('');
  const renderCard = (card, index) => {
    const [name, status, note, ...files] = card;
    const captions = file => file.startsWith('resonance-buildings-modeling/') ? 'Художественные виды · здание' : file.startsWith('resonance-buildings-review/') || file === 'resonance-plasma-extractor-directions/a.png' || ['core-heart-directions/01-a.png','assembly-materials/01-a.png'].includes(file) ? 'Основной визуал' : ['core-heart-directions/07-a-model-sheet.png','assembly-materials/07-a-model-sheet.png'].includes(file) ? 'Художественные виды · здание' : file.includes('guardian-directions/08-') ? 'Художественные виды · наземная форма' : file.includes('guardian-directions/09-') ? 'Художественные виды · ПВО' : /-hero(?:-v\d+)?\.png$/.test(file) ? 'Основной визуал' : file.endsWith('/resonance-transport-reboot/g.png') || file === 'resonance-transport-reboot/g.png' ? 'Основной визуал' : file.includes('g-attached-model-sheet') ? 'Художественные виды · с закрытой капсулой' : file.includes('g-empty-model-sheet') ? 'Художественные виды · без капсулы' : file.endsWith('golem-shield-motion.svg') ? 'Одна ведущая ось · контроль траектории центральной плиты' : file.includes('geometry-guides/') ? 'Контрольные проекции одной сетки · вспомогательный каркас' : file.includes('-deployed-model-sheet') ? 'Художественные виды · раскрытая форма' : file.startsWith('resonance-final/') ? 'Художественные виды · базовая форма' : 'Дополнительный референс';
    const imgs = files.map(file => `<figure><button class="image-button" data-image="${esc(file)}" aria-label="Увеличить: ${esc(name)} — ${captions(file)}"><img src="${images.get(file)}" alt="${esc(name)} — ${captions(file)}" loading="lazy" width="1800" height="1200"></button><figcaption>${captions(file)} · <a href="concepts/${file}">PNG-оригинал</a></figcaption></figure>`).join('');
    const unit = units.find(unit => unit.label === name);
    const geometryLinks = unit ? `<p class="small">${(unit.blockouts || []).map((file, i) => `<a href="concepts/resonance-final/${file}">OBJ каркаса${i ? ' · раскрытая форма' : ''}</a>`).join(' · ')} · <a href="concepts/resonance-final/CONSISTENCY.md">Результаты сверки и ограничения</a></p>` : '';
    return `<article class="concept" id="concept-${index}"><div class="card-title"><h3>${esc(name)}</h3><span class="badge ${status === 'Принято' ? 'accepted' : status === 'Направление' ? 'direction' : 'pending'}">${status}</span></div><p>${esc(note)}</p>${imgs}${geometryLinks}</article>`;
  };
  const approved = cards.map((c, i) => [c, i]).filter(([c]) => ['Принято', 'Направление'].includes(c[1])).map(([c, i]) => renderCard(c, i)).join('');
  const pending = cards.map((c, i) => [c, i]).filter(([c]) => !['Принято', 'Направление'].includes(c[1])).map(([c, i]) => renderCard(c, i)).join('');
  const template = fs.readFileSync(path.join(__dirname, 'handbook-template.html'), 'utf8');
  const output = template.replace('<!--TOC-->', toc).replace('<!--APPROVED-->', approved).replace('<!--PENDING-->', pending).replace('<!--BODY-->', body).replace('<!--DATE-->', new Intl.DateTimeFormat('ru-RU', { timeZone: 'Europe/Budapest', dateStyle: 'long' }).format(new Date()));
  fs.writeFileSync(path.join(root, 'docs/PROJECT.html'), output);
  console.log(`Built docs/PROJECT.html: ${anchors.length} main sections, ${cards.length} cards, ${images.size} embedded images, ${(Buffer.byteLength(output) / 1048576).toFixed(1)} MB`);
}
main().catch(error => { console.error(error); process.exitCode = 1; });
