using System;
using System.Collections.Generic;
using System.Reflection;
using CalmSpace.Workshop;
using UnityEditor;
using UnityEngine;

namespace CalmSpace.Editor
{
    /// <summary>
    /// Single deterministic authoring source for workshop prose. The generated
    /// asset is intentionally not hand-authored.
    /// </summary>
    internal static class CalmSpaceWorkshopTextBuilder
    {
        public const string CatalogPath =
            "Assets/CalmSpace/Config/WorkshopTextCatalog.asset";

        private static readonly WorkshopTextEntry[] Entries =
        {
            E("chapter.cozy-workshop.title", "Old Family Workshop", "Стара сімейна майстерня", "Старая семейная мастерская"),
            E("memory.summer-trail.title", "Stones from the summer trail", "Камінці з літньої стежки", "Камни с летней тропы"),
            E("memory.summer-trail.body", "We brought a small treasure home from every walk.", "З кожної прогулянки ми приносили додому маленький скарб.", "С каждой прогулки мы приносили домой маленькое сокровище."),
            E("memory.fix-everything.title", "Here, we could fix anything", "Тут уміли полагодити все", "Здесь умели чинить всё"),
            E("memory.fix-everything.body", "Measure twice. Be patient. Leave it kinder than you found it.", "Двічі відмір. Не поспішай. Залиш річ кращою, ніж вона була.", "Дважды отмерь. Не спеши. Оставь вещь лучше, чем она была."),
            E("memory.open-windows.title", "Open the windows again", "Знову відкрити вікна", "Снова открыть окна"),
            E("memory.open-windows.body", "The room was waiting for voices, tea and summer air.", "Кімната чекала на голоси, чай і літнє повітря.", "Комната ждала голосов, чая и летнего воздуха."),
            E("postcard.family-tea.title", "The family tea card", "Листівка сімейного чаю", "Открытка семейного чаепития"),
            E("postcard.family-tea.body", "One spoon for the pot, and time enough to sit together.", "Одна ложка для чайника — і час, щоб посидіти разом.", "Одна ложка для чайника — и время посидеть вместе."),
            E("finale.mom.line-1", "I knew the light would find this room again.", "Я знала, що світло знову знайде цю кімнату.", "Я знала, что свет снова найдёт эту комнату."),
            E("finale.mom.line-2", "Let’s open the kitchen next. Everyone will be here soon.", "Далі відкриємо кухню. Скоро всі будуть тут.", "Дальше откроем кухню. Скоро все будут здесь."),
            E("teaser.kitchen", "Next room: the family kitchen", "Наступна кімната: сімейна кухня", "Следующая комната: семейная кухня"),
            E("beat.clear-passage.title", "Clear the passage", "Звільнити прохід", "Освободить проход"),
            E("beat.clear-passage.result", "The way to the workbench is clear.", "Шлях до верстака вільний.", "Путь к верстаку свободен."),
            E("beat.pebble-shelf.title", "Arrange the trail stones", "Розкласти камінці зі стежки", "Разложить камни с тропы"),
            E("beat.pebble-shelf.result", "Every stone has found its place.", "Кожен камінець знайшов своє місце.", "Каждый камень нашёл своё место."),
            E("beat.tea-drawer.title", "Restore the tea drawer", "Відновити чайну шухляду", "Восстановить чайный ящик"),
            E("beat.tea-drawer.result", "Family tea is close at hand again.", "Сімейний чай знову поруч.", "Семейный чай снова под рукой."),
            E("beat.paint-shelf.title", "Bring order to the paint shelf", "Навести лад на полиці з фарбами", "Навести порядок на полке с красками"),
            E("beat.paint-shelf.result", "The colors are ready for new ideas.", "Фарби готові до нових ідей.", "Краски готовы к новым идеям."),
            E("beat.fastener-tray.title", "Return the tools", "Повернути інструменти на місця", "Вернуть инструменты на места"),
            E("beat.fastener-tray.result", "Everything needed to mend and make is back.", "Усе для ремонту й творчості знову на місці.", "Всё для ремонта и творчества снова на месте."),
            E("beat.warm-workbench.title", "Reveal the warm workbench", "Повернути тепло верстаку", "Вернуть верстаку тепло"),
            E("beat.warm-workbench.result", "Warm wood has appeared beneath the dust.", "Під пилом знову видно тепле дерево.", "Под пылью снова видно тёплое дерево."),
            E("beat.cabinet-hinge.title", "Repair the old cabinet", "Полагодити стару шафу", "Починить старый шкаф"),
            E("beat.cabinet-hinge.result", "The cabinet closes softly again.", "Шафа знову зачиняється тихо.", "Шкаф снова закрывается тихо."),
            E("beat.open-window.title", "Let the light in", "Впустити світло", "Впустить свет"),
            E("beat.open-window.result", "Morning light fills the workshop.", "Ранкове світло наповнює майстерню.", "Утренний свет наполняет мастерскую."),
            E("home.start", "Start", "Почати", "Начать"),
            E("home.catalog", "All spaces", "Усі простори", "Все пространства"),
            E("home.album", "Family album", "Сімейний альбом", "Семейный альбом"),
            E("home.decor", "Make it yours", "Зробити по-своєму", "Обустроить по-своему"),
            E("home.settings", "Settings", "Налаштування", "Настройки"),
            E("home.daily-care", "Brew family tea", "Заварити сімейний чай", "Заварить семейный чай"),
            E("home.view-workshop", "View the workshop", "Подивитися майстерню", "Посмотреть мастерскую"),
            E("home.chapter-complete", "The workshop is ready for everyone.", "Майстерня готова зустрічати всіх.", "Мастерская готова встретить всех."),
            E("load.failure.title", "This space needs one calm moment.", "Цьому простору потрібна спокійна мить.", "Этому пространству нужна спокойная минута."),
            E("load.failure.retry", "Try again", "Спробувати ще раз", "Попробовать снова"),
            E("save.retry", "Retry save", "Повторити збереження", "Повторить сохранение"),
            E("save.return-without", "Return without saving", "Повернутися без збереження", "Вернуться без сохранения"),
            E("album.title", "Our family album", "Наш сімейний альбом", "Наш семейный альбом"),
            E("album.locked", "A memory is still waiting here.", "Тут іще чекає спогад.", "Здесь ещё ждёт воспоминание."),
            E("reveal.fallback.title", "The workshop changed while you were away.", "Майстерня змінилася, поки вас не було.", "Мастерская изменилась, пока вас не было."),
            E("common.skip", "Skip", "Пропустити", "Пропустить"),
            E("common.close", "Close", "Закрити", "Закрыть"),
            E("rewarded.unlock", "Watch to unlock this decor", "Переглянути й відкрити цей декор", "Посмотреть и открыть этот декор"),
            E("rewarded.unavailable", "This option is resting for now.", "Ця можливість поки відпочиває.", "Эта возможность пока отдыхает."),
            E("rewarded.closed", "Nothing changed. You can try again later.", "Нічого не змінилося. Можна спробувати пізніше.", "Ничего не изменилось. Можно попробовать позже."),
            E("decor.slot.workbench", "Workbench accent", "Акцент верстака", "Акцент верстака"),
            E("decor.slot.light", "Warm light", "Тепле світло", "Тёплый свет"),
            E("decor.soft-fern", "Soft fern", "Ніжна папороть", "Нежный папоротник"),
            E("decor.river-stones", "River stones", "Річкове каміння", "Речные камни"),
            E("decor.clay-vase", "Clay vase", "Глиняна ваза", "Глиняная ваза"),
            E("decor.moon-glaze-vase", "Moon-glaze vase", "Ваза з місячною поливою", "Ваза с лунной глазурью"),
            E("decor.linen-shade", "Linen shade", "Лляний абажур", "Льняной абажур"),
            E("decor.warm-lantern", "Warm lantern", "Теплий ліхтар", "Тёплый фонарь"),
            E("decor.paper-light", "Paper light", "Паперовий світильник", "Бумажный светильник"),
            E("decor.amber-lamp", "Amber workshop lamp", "Бурштинова лампа майстерні", "Янтарная лампа мастерской"),
            E("daily.title", "Brew family tea", "Заварити сімейний чай", "Заварить семейный чай"),
            E("daily.instruction", "Add the tea, pour gently, then stir three calm circles.", "Додай чай, обережно налий воду й зроби три спокійні кола ложкою.", "Добавь чай, аккуратно налей воду и сделай ложкой три спокойных круга."),
            E("daily.complete", "Tea is ready. A quiet place is waiting.", "Чай готовий. На тебе чекає тихе місце.", "Чай готов. Тебя ждёт тихое место."),
            E("relax-pass.title", "Relax Pass", "Relax Pass", "Relax Pass")
        };

        public static IReadOnlyList<string> RequiredKeys { get; } =
            Array.ConvertAll(Entries, entry => entry.Key);

        public static WorkshopTextCatalog CreateOrUpdate()
        {
            WorkshopTextCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorkshopTextCatalog>(
                    CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WorkshopTextCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SetPrivateField(catalog, "_entries", CreateEntries());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static WorkshopTextEntry[] CreateEntries()
        {
            var result = new WorkshopTextEntry[Entries.Length];
            for (var index = 0; index < Entries.Length; index++)
            {
                WorkshopTextEntry entry = Entries[index];
                result[index] = E(
                    entry.Key,
                    entry.English,
                    entry.Ukrainian,
                    entry.Russian);
            }

            return result;
        }

        private static WorkshopTextEntry E(
            string key,
            string english,
            string ukrainian,
            string russian)
        {
            return new WorkshopTextEntry(key, english, ukrainian, russian);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(
                    target.GetType().FullName,
                    fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
