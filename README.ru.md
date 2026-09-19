# DS3 Appearance Preset Tool
Тул для экспорта и импорта внешности персонажа в Dark Souls III.
Аналог [DSAppearancePresetTool](https://github.com/BobDoleOwndU/DSAppearancePresetTool)
(BobDoleOwndU), который делает то же самое для Dark Souls Remastered/PTDE.

*[English version](README.md)*

![окно](docs/window.png)

## Как пользоваться
### Экспорт
1. Запусти тул до или после Dark Souls III — он подцепится сам.
2. Загрузи персонажа, которого хочешь выгрузить.
3. Нажми *Export Appearance Data*.
4. Выбери, куда сохранить файл.
5. Готово: внешность лежит в файле .ds3chr.

### Импорт
1. Запусти тул и загрузи персонажа, которого меняешь.
2. Нажми *Import Appearance Data*.
3. Выбери файл .ds3chr.
4. Тул откроет **Alter Appearance** (меню Розарии) и запишет внешность туда.
5. Подтверди в меню, чтобы изменения остались.

### Пол
Выбор в списке *Gender* уходит в игру сразу. Пол — то, за что берёт плату Розария,
так что менять его лучше с открытым меню смены внешности.

### Заметки
Нужен .NET 8 (Windows Desktop). Если не удаётся открыть процесс игры — запусти от
администратора.

Тул пишет в память игры, поэтому **в онлайне с ним играть не надо**, и сделай бэкап
сейва. Как оно устроено внутри: [docs/internals.md](docs/internals.md).

## Кредиты
Igromanru — офсеты внешности и скрипт Alter Appearance в его Cheat Table.
BobDoleOwndU — DSAppearancePresetTool, с которого списана форма этого тула.

Лицензия MIT, см. `LICENSE`.
