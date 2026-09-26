# -*- coding: utf-8 -*-
"""Generates one C# table per language from a single source of truth."""
import io, os, sys

OUT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                   "..", "..", "Seety", "Localization", "Strings"))

LOCALES = [
    ("En", "en-US"), ("De", "de-DE"), ("Es", "es-ES"), ("Fr", "fr-FR"),
    ("It", "it-IT"), ("Ja", "ja-JP"), ("Ko", "ko-KR"), ("Pl", "pl-PL"),
    ("PtBr", "pt-BR"), ("Ru", "ru-RU"), ("ZhHans", "zh-HANS"), ("ZhHant", "zh-HANT"),
]

# key -> list of 12 strings, in LOCALES order.
D = {}


def add(key, *values):
    assert len(values) == len(LOCALES), (key, len(values))
    D[key] = list(values)


# ---------------------------------------------------------------- options page
add("option.title",
    "Seety", "Seety", "Seety", "Seety", "Seety", "Seety",
    "Seety", "Seety", "Seety", "Seety", "Seety", "Seety")
add("option.tab.Main",
    "Main", "Allgemein", "General", "Général", "Generale", "メイン",
    "기본", "Główne", "Principal", "Основное", "主要", "主要")
add("option.group.DisplayGroup",
    "Display", "Anzeige", "Visualización", "Affichage", "Visualizzazione", "表示",
    "표시", "Wyświetlanie", "Exibição", "Отображение", "显示", "顯示")
add("option.group.FundsGroup",
    "Funds", "Finanzen", "Fondos", "Fonds", "Fondi", "資金",
    "자금", "Fundusze", "Fundos", "Средства", "资金", "資金")
add("option.label.ShowStrip",
    "Show the strip", "Leiste anzeigen", "Mostrar la barra", "Afficher la barre",
    "Mostra la barra", "バーを表示", "바 표시", "Pokaż pasek",
    "Mostrar a barra", "Показывать панель", "显示状态条", "顯示狀態條")
add("option.desc.ShowStrip",
    "Show or hide the whole bar. Click any entry on it to open the matching in-game info view.",
    "Blendet die gesamte Leiste ein oder aus. Ein Klick auf einen Eintrag öffnet die passende Infoansicht.",
    "Muestra u oculta toda la barra. Haz clic en cualquier lectura para abrir su vista de información.",
    "Affiche ou masque toute la barre. Cliquez sur une lecture pour ouvrir la vue d'information correspondante.",
    "Mostra o nasconde tutta la barra. Clicca una lettura per aprire la relativa vista informativa.",
    "バー全体の表示を切り替えます。項目をクリックすると対応するインフォビューが開きます。",
    "바 전체를 표시하거나 숨깁니다. 항목을 클릭하면 해당 정보 보기가 열립니다.",
    "Pokazuje lub ukrywa cały pasek. Kliknij dowolny odczyt, aby otworzyć powiązany widok informacji.",
    "Mostra ou oculta a barra inteira. Clique em qualquer leitura para abrir a visão de informação correspondente.",
    "Показывает или скрывает всю панель. Нажмите на любой показатель, чтобы открыть соответствующий режим информации.",
    "显示或隐藏整个状态条。点击任意读数可打开对应的信息视图。",
    "顯示或隱藏整個狀態條。點擊任一讀數可開啟對應的資訊檢視。")
add("option.label.HighlightProblems",
    "Highlight problems", "Probleme hervorheben", "Resaltar problemas", "Mettre en évidence les problèmes",
    "Evidenzia i problemi", "問題を強調表示", "문제 강조", "Podświetlaj problemy",
    "Destacar problemas", "Выделять проблемы", "突出显示问题", "突顯問題")
add("option.desc.HighlightProblems",
    "Colour an entry amber or red when it crosses a sensible limit. The numbers themselves never change.",
    "Färbt einen Eintrag gelb oder rot, wenn er eine sinnvolle Grenze überschreitet. Die Zahlen selbst ändern sich nie.",
    "Colorea una lectura en ámbar o rojo cuando cruza un límite razonable. Los números nunca cambian.",
    "Colore une lecture en orange ou en rouge lorsqu'elle dépasse une limite raisonnable. Les chiffres ne changent jamais.",
    "Colora una lettura di ambra o rosso quando supera un limite ragionevole. I numeri non cambiano mai.",
    "妥当な限度を超えた項目を黄色や赤で表示します。数値そのものは変わりません。",
    "적절한 한계를 넘으면 항목을 주황색이나 빨간색으로 표시합니다. 수치 자체는 바뀌지 않습니다.",
    "Koloruje odczyt na bursztynowo lub czerwono po przekroczeniu rozsądnej granicy. Same liczby nigdy się nie zmieniają.",
    "Colore uma leitura em âmbar ou vermelho quando ela cruza um limite razoável. Os números nunca mudam.",
    "Окрашивает показатель в жёлтый или красный при выходе за разумный предел. Сами числа не меняются.",
    "当读数越过合理界限时将其标为琥珀色或红色。数字本身永远不变。",
    "當讀數越過合理界限時將其標為琥珀色或紅色。數字本身永遠不變。")
add("option.label.FundsAmount",
    "Amount", "Betrag", "Cantidad", "Montant", "Importo", "金額",
    "금액", "Kwota", "Quantia", "Сумма", "金额", "金額")
add("option.desc.FundsAmount",
    "How much to add to the city treasury. A negative number takes money away instead.",
    "Wie viel der Stadtkasse hinzugefügt wird. Ein negativer Wert nimmt stattdessen Geld weg.",
    "Cuánto añadir a la tesorería de la ciudad. Un número negativo retira dinero.",
    "Montant à ajouter au trésor de la ville. Un nombre négatif retire de l'argent.",
    "Quanto aggiungere alle casse della città. Un numero negativo toglie denaro.",
    "市の財政に加える金額です。負の数を入れると差し引かれます。",
    "도시 재정에 더할 금액입니다. 음수를 넣으면 대신 차감됩니다.",
    "Ile dodać do skarbca miasta. Liczba ujemna zamiast tego odejmuje pieniądze.",
    "Quanto adicionar ao tesouro da cidade. Um número negativo retira dinheiro.",
    "Сколько добавить в казну города. Отрицательное число, наоборот, снимает деньги.",
    "向城市金库增加的金额。负数则会扣除资金。",
    "向城市金庫增加的金額。負數則會扣除資金。")
add("option.label.AddFunds",
    "Apply to treasury", "Auf Stadtkasse anwenden", "Aplicar a la tesorería", "Appliquer au trésor",
    "Applica alle casse", "財政に反映", "재정에 적용", "Zastosuj do skarbca",
    "Aplicar ao tesouro", "Применить к казне", "应用到金库", "套用到金庫")
add("option.desc.AddFunds",
    "This is the one control in Seety that changes your city rather than reporting on it.",
    "Dies ist das einzige Element in Seety, das deine Stadt verändert statt nur über sie zu berichten.",
    "Este es el único control de Seety que cambia tu ciudad en lugar de informar sobre ella.",
    "C'est la seule commande de Seety qui modifie votre ville au lieu d'en rendre compte.",
    "È l'unico controllo di Seety che modifica la città invece di riferire su di essa.",
    "Seety の中で唯一、都市を報告するのではなく変更する操作です。",
    "Seety에서 도시를 보고하는 대신 변경하는 유일한 조작입니다.",
    "To jedyny element Seety, który zmienia miasto, zamiast tylko o nim informować.",
    "Este é o único controle do Seety que altera sua cidade em vez de apenas relatá-la.",
    "Это единственный элемент Seety, который изменяет город, а не сообщает о нём.",
    "这是 Seety 中唯一会改变城市而非报告城市的控件。",
    "這是 Seety 中唯一會改變城市而非報告城市的控制項。")

add("option.label.ToolbarTrends",
    "Show change on the bottom bar", "Veränderung in der unteren Leiste zeigen",
    "Mostrar la variación en la barra inferior", "Afficher la variation sur la barre du bas",
    "Mostra la variazione nella barra in basso", "下部バーに増減を表示",
    "하단 바에 증감 표시", "Pokaż zmianę na dolnym pasku",
    "Mostrar a variação na barra inferior", "Показывать изменение на нижней панели",
    "在底部栏显示变化", "在底部列顯示變化")
add("option.desc.ToolbarTrends",
    "Prints the change beside the population and money figures on the game's own bottom bar, which otherwise only shows it while you hover. The number is the game's own, not one Seety works out. This is the only thing Seety draws outside its own bar, so turn it off if a game update ever makes it look wrong.",
    "Zeigt die Veränderung neben Bevölkerung und Geld in der unteren Leiste des Spiels an, die sie sonst nur beim Daraufzeigen einblendet. Die Zahl stammt vom Spiel, nicht von Seety. Das ist das Einzige, was Seety außerhalb der eigenen Leiste zeichnet - bei Problemen nach einem Spiel-Update einfach ausschalten.",
    "Muestra la variación junto a la población y el dinero en la barra inferior del juego, que de otro modo solo la enseña al pasar el cursor. La cifra es del propio juego, no calculada por Seety. Es lo único que Seety dibuja fuera de su barra: desactívalo si una actualización del juego lo estropea.",
    "Affiche la variation à côté de la population et de l'argent sur la barre du bas du jeu, qui ne la montre sinon qu'au survol. Le chiffre vient du jeu, pas d'un calcul de Seety. C'est la seule chose que Seety dessine hors de sa propre barre : désactivez-la si une mise à jour du jeu la met en défaut.",
    "Mostra la variazione accanto a popolazione e denaro nella barra in basso del gioco, che altrimenti la mostra solo al passaggio del cursore. Il numero è quello del gioco, non calcolato da Seety. È l'unica cosa che Seety disegna fuori dalla propria barra: disattivala se un aggiornamento del gioco la rende sbagliata.",
    "ゲーム下部バーの人口と資金の横に増減を表示します。通常はカーソルを合わせたときしか表示されません。数値はゲーム自身のもので、Seety が計算したものではありません。Seety が自分のバーの外に描く唯一の要素なので、ゲームの更新で表示が崩れたらオフにしてください。",
    "게임 하단 바의 인구와 자금 옆에 증감을 표시합니다. 평소에는 마우스를 올려야만 보입니다. 이 수치는 게임이 제공하는 값이며 Seety가 계산한 것이 아닙니다. Seety가 자체 바 밖에 그리는 유일한 요소이므로, 게임 업데이트 후 이상하면 끄세요.",
    "Pokazuje zmianę obok liczby mieszkańców i pieniędzy na dolnym pasku gry, który w przeciwnym razie wyświetla ją tylko po najechaniu kursorem. Liczba pochodzi z gry, nie z obliczeń Seety. To jedyna rzecz, którą Seety rysuje poza własnym paskiem - wyłącz, jeśli aktualizacja gry ją zepsuje.",
    "Mostra a variação ao lado da população e do dinheiro na barra inferior do jogo, que de outro modo só a exibe ao passar o cursor. O número é do próprio jogo, não calculado pelo Seety. É a única coisa que o Seety desenha fora da própria barra: desative se uma atualização do jogo a deixar errada.",
    "Показывает изменение рядом с населением и деньгами на нижней панели игры, которая иначе выводит его только при наведении. Число берётся у самой игры, а не рассчитывается Seety. Это единственное, что Seety рисует за пределами своей панели, - отключите, если после обновления игры оно отображается неверно.",
    "在游戏底部栏的人口和资金旁显示变化，否则只有悬停时才看得到。该数字来自游戏本身，不是 Seety 计算的。这是 Seety 唯一画在自己栏之外的内容，若游戏更新后显示异常请关闭。",
    "在遊戲底部列的人口與資金旁顯示變化，否則只有停留游標時才看得到。該數字來自遊戲本身，不是 Seety 計算的。這是 Seety 唯一畫在自己列之外的內容，若遊戲更新後顯示異常請關閉。")
add("option.label.IconOutline",
    "White outline on icons", "Weißer Rand um Symbole", "Contorno blanco en los iconos",
    "Contour blanc sur les icônes", "Contorno bianco sulle icone", "アイコンの白フチ",
    "아이콘 흰색 테두리", "Biały kontur ikon", "Contorno branco nos ícones",
    "Белый контур значков", "图标白色描边", "圖示白色描邊")
add("option.desc.IconOutline",
    "Draws a white edge around each icon on the bar, so a dark icon stays legible over a dark building. Turn it off for a flatter look; the readings do not change either way.",
    "Zeichnet einen weißen Rand um jedes Symbol der Leiste, damit ein dunkles Symbol auch über einem dunklen Gebäude lesbar bleibt. Ausschalten für ein flacheres Bild; an den Werten ändert sich nichts.",
    "Dibuja un borde blanco alrededor de cada icono de la barra, para que un icono oscuro siga siendo legible sobre un edificio oscuro. Desactívalo para un aspecto más plano; las lecturas no cambian.",
    "Trace un contour blanc autour de chaque icône de la barre, pour qu'une icône sombre reste lisible sur un bâtiment sombre. Désactivez-le pour un rendu plus plat ; les valeurs ne changent pas.",
    "Disegna un bordo bianco attorno a ogni icona della barra, così un'icona scura resta leggibile sopra un edificio scuro. Disattivalo per un aspetto più piatto; le letture non cambiano.",
    "バーの各アイコンに白いフチを描き、暗い建物の上でも暗いアイコンが読み取れるようにします。オフにするとフラットな見た目になります。数値は変わりません。",
    "바의 각 아이콘에 흰색 테두리를 그려 어두운 건물 위에서도 어두운 아이콘이 잘 보이게 합니다. 끄면 더 평평한 모습이 됩니다. 수치는 바뀌지 않습니다.",
    "Rysuje biały kontur wokół każdej ikony na pasku, dzięki czemu ciemna ikona pozostaje czytelna na tle ciemnego budynku. Wyłącz, aby uzyskać płaszczy wygląd; odczyty się nie zmieniają.",
    "Desenha uma borda branca em volta de cada ícone da barra, para que um ícone escuro continue legível sobre um edifício escuro. Desative para um visual mais plano; as leituras não mudam.",
    "Рисует белый контур вокруг каждого значка на панели, чтобы тёмный значок оставался различим на фоне тёмного здания. Выключите для более плоского вида; показатели не меняются.",
    "在状态条的每个图标周围绘制白色描边，让深色图标在深色建筑上依然清晰。关闭可获得更扁平的外观；读数不会改变。",
    "在狀態條的每個圖示周圍繪製白色描邊，讓深色圖示在深色建築上依然清晰。關閉可獲得更扁平的外觀；讀數不會改變。")

add("option.label.GameButtonStyle",
    "Draw the bar as game buttons", "Leiste als Spielschaltflächen", "Barra con botones del juego",
    "Barre en boutons du jeu", "Barra con i pulsanti del gioco", "バーをゲームのボタン風に",
    "바를 게임 버튼 모양으로", "Pasek jako przyciski gry", "Barra com botões do jogo",
    "Панель в виде кнопок игры", "状态条使用游戏按钮样式", "狀態條使用遊戲按鈕樣式")
add("option.desc.GameButtonStyle",
    "Shows each reading as one of the game's own blue buttons, like the row at the top left, instead of on one dark panel. Colour, size and corners come from the game. The readings do not change.",
    "Zeigt jeden Wert als eine der blauen Schaltflächen des Spiels, wie die Reihe oben links, statt auf einer dunklen Leiste. Farbe, Größe und Ecken stammen vom Spiel. An den Werten ändert sich nichts.",
    "Muestra cada lectura como uno de los botones azules del juego, como la fila de arriba a la izquierda, en lugar de sobre un panel oscuro. El color, el tamaño y las esquinas vienen del juego. Las lecturas no cambian.",
    "Affiche chaque valeur comme l'un des boutons bleus du jeu, comme la rangée en haut à gauche, au lieu d'un panneau sombre. La couleur, la taille et les coins viennent du jeu. Les valeurs ne changent pas.",
    "Mostra ogni lettura come uno dei pulsanti blu del gioco, come la fila in alto a sinistra, invece che su un pannello scuro. Colore, dimensione e angoli vengono dal gioco. Le letture non cambiano.",
    "各数値を、暗いパネルではなく左上の列と同じゲームの青いボタンとして表示します。色・サイズ・角はゲームのものを使います。数値は変わりません。",
    "각 수치를 어두운 패널 대신 왼쪽 위 줄과 같은 게임의 파란 버튼으로 표시합니다. 색, 크기, 모서리는 게임에서 가져옵니다. 수치는 바뀌지 않습니다.",
    "Pokazuje każdy odczyt jako jeden z niebieskich przycisków gry, jak rząd w lewym górnym rogu, zamiast na ciemnym panelu. Kolor, rozmiar i narożniki pochodzą z gry. Odczyty się nie zmieniają.",
    "Mostra cada leitura como um dos botões azuis do próprio jogo, como a fileira no canto superior esquerdo, em vez de um painel escuro. Cor, tamanho e cantos vêm do jogo. As leituras não mudam.",
    "Показывает каждый показатель как одну из синих кнопок игры, как ряд в левом верхнем углу, а не на тёмной панели. Цвет, размер и углы берутся из игры. Показатели не меняются.",
    "将每个读数显示为游戏自带的蓝色按钮，就像左上角那一排，而不是放在深色面板上。颜色、尺寸和圆角都来自游戏。读数不会改变。",
    "將每個讀數顯示為遊戲內建的藍色按鈕，就像左上角那一排，而不是放在深色面板上。顏色、尺寸和圓角都來自遊戲。讀數不會改變。")

add("option.label.DarkGameButtons",
    "Dark game buttons", "Dunkle Spielschaltflächen", "Botones del juego oscuros",
    "Boutons du jeu sombres", "Pulsanti del gioco scuri", "ゲームのボタンを暗く",
    "게임 버튼 어둡게", "Ciemne przyciski gry", "Botões do jogo escuros",
    "Тёмные кнопки игры", "深色游戏按钮", "深色遊戲按鈕")
add("option.desc.DarkGameButtons",
    "Draws the game's blue buttons, like the row at the top left, in the dark blue of the bottom bar. It changes every button built with the game's own button, including other mods' ones; buttons a mod draws itself stay as they are. Selected buttons keep their colour. Turning it off restores the game's look.",
    "Zeichnet die blauen Schaltflächen des Spiels, wie die Reihe oben links, im dunklen Blau der unteren Leiste. Das betrifft jede Schaltfläche, die mit der Spielschaltfläche gebaut ist, auch die anderer Mods; selbst gezeichnete Schaltflächen bleiben unverändert. Ausgewählte Schaltflächen behalten ihre Farbe. Ausschalten stellt das Aussehen des Spiels wieder her.",
    "Dibuja los botones azules del juego, como la fila de arriba a la izquierda, en el azul oscuro de la barra inferior. Cambia todos los botones hechos con el botón del propio juego, también los de otros mods; los que un mod dibuja por su cuenta no cambian. Los botones seleccionados conservan su color. Al desactivarlo vuelve el aspecto del juego.",
    "Dessine les boutons bleus du jeu, comme la rangée en haut à gauche, dans le bleu foncé de la barre du bas. Cela change tous les boutons construits avec le bouton du jeu, y compris ceux d'autres mods ; ceux qu'un mod dessine lui-même restent tels quels. Les boutons sélectionnés gardent leur couleur. Le désactiver rétablit l'apparence du jeu.",
    "Disegna i pulsanti blu del gioco, come la fila in alto a sinistra, nel blu scuro della barra in basso. Cambia ogni pulsante costruito con il pulsante del gioco, anche quelli di altre mod; quelli che una mod disegna da sé restano come sono. I pulsanti selezionati mantengono il loro colore. Disattivandola torna l'aspetto del gioco.",
    "左上の列のようなゲームの青いボタンを、下部バーの濃い青で描きます。ゲーム自身のボタンで作られたボタンはすべて変わり、他の MOD のものも含みます。MOD が独自に描いたボタンは変わりません。選択中のボタンは色を保ちます。オフにするとゲームの見た目に戻ります。",
    "왼쪽 위 줄 같은 게임의 파란 버튼을 하단 바의 짙은 파랑으로 그립니다. 게임 자체 버튼으로 만든 모든 버튼이 바뀌며 다른 모드의 버튼도 포함됩니다. 모드가 직접 그린 버튼은 그대로입니다. 선택된 버튼은 색을 유지합니다. 끄면 게임 본래 모습으로 돌아갑니다.",
    "Rysuje niebieskie przyciski gry, jak rząd w lewym górnym rogu, w ciemnym błękicie dolnego paska. Zmienia każdy przycisk zbudowany z przycisku gry, także w innych modach; przyciski rysowane przez mod samodzielnie pozostają bez zmian. Zaznaczone przyciski zachowują kolor. Wyłączenie przywraca wygląd gry.",
    "Desenha os botões azuis do jogo, como a fileira no canto superior esquerdo, no azul-escuro da barra inferior. Muda todos os botões feitos com o botão do próprio jogo, inclusive os de outros mods; os que um mod desenha por conta própria ficam como estão. Botões selecionados mantêm a cor. Desativar restaura a aparência do jogo.",
    "Рисует синие кнопки игры, как ряд в левом верхнем углу, в тёмно-синем цвете нижней панели. Меняются все кнопки, созданные на основе кнопки игры, включая кнопки других модов; кнопки, которые мод рисует сам, остаются прежними. Выбранные кнопки сохраняют цвет. Выключение возвращает вид игры.",
    "将游戏的蓝色按钮（如左上角那一排）改为底部栏的深蓝色。所有使用游戏自带按钮的按钮都会改变，包括其他模组的按钮；模组自行绘制的按钮保持不变。选中的按钮保留原色。关闭后恢复游戏外观。",
    "將遊戲的藍色按鈕（如左上角那一排）改為底部列的深藍色。所有使用遊戲內建按鈕的按鈕都會改變，包括其他模組的按鈕；模組自行繪製的按鈕保持不變。選取的按鈕保留原色。關閉後恢復遊戲外觀。")

add("option.label.BuildingReasons",
    "Why a building struggles", "Warum ein Gebäude Probleme hat", "Por qué un edificio tiene problemas",
    "Pourquoi un bâtiment peine", "Perché un edificio è in difficoltà", "建物の不調の理由",
    "건물이 어려운 이유", "Dlaczego budynek ma problemy", "Por que um edifício tem problemas",
    "Почему здание испытывает трудности", "建筑为何表现不佳", "建築為何表現不佳")
add("option.desc.BuildingReasons",
    "When the cursor rests on a building, lists up to three efficiency losses holding it back, with what each one costs. The problems already shown as icons over the building are not repeated, unless you hid those icons. Nothing appears when nothing is wrong. Only with the selection tool.",
    "Wenn der Mauszeiger auf einem Gebäude ruht, werden bis zu drei Effizienzverluste mit ihrem Anteil aufgeführt. Probleme, die schon als Symbole über dem Gebäude stehen, werden nicht wiederholt, außer du hast diese Symbole ausgeblendet. Ist nichts falsch, erscheint nichts. Nur mit dem Auswahlwerkzeug.",
    "Al dejar el cursor sobre un edificio, enumera hasta tres pérdidas de eficiencia que lo frenan, con lo que cuesta cada una. Los problemas que ya se ven como iconos sobre el edificio no se repiten, salvo que hayas ocultado esos iconos. Si no pasa nada, no aparece nada. Solo con la herramienta de selección.",
    "Quand le curseur reste sur un bâtiment, indique jusqu'à trois pertes d'efficacité qui le freinent, avec ce que chacune coûte. Les problèmes déjà affichés en icônes au-dessus du bâtiment ne sont pas répétés, sauf si vous avez masqué ces icônes. Rien n'apparaît si tout va bien. Uniquement avec l'outil de sélection.",
    "Quando il cursore si ferma su un edificio, elenca fino a tre perdite di efficienza che lo frenano, con quanto costa ciascuna. I problemi già mostrati come icone sopra l'edificio non vengono ripetuti, a meno che tu abbia nascosto quelle icone. Se va tutto bene non compare nulla. Solo con lo strumento di selezione.",
    "カーソルを建物に置くと、その建物を妨げている効率の低下を最大 3 つ、それぞれの割合とともに表示します。建物の上にアイコンで表示済みの問題は、アイコンを非表示にしていない限り繰り返しません。問題がなければ何も表示されません。選択ツール使用時のみ。",
    "커서를 건물 위에 두면 건물을 방해하는 효율 손실을 최대 세 가지, 각각의 비율과 함께 보여 줍니다. 건물 위에 이미 아이콘으로 표시된 문제는 그 아이콘을 숨기지 않은 한 반복하지 않습니다. 문제가 없으면 아무것도 나타나지 않습니다. 선택 도구에서만.",
    "Gdy kursor zatrzyma się na budynku, wymienia do trzech strat wydajności, które go hamują, wraz z ich wielkością. Problemy widoczne już jako ikony nad budynkiem nie są powtarzane, chyba że ukryłeś te ikony. Gdy wszystko w porządku, nic się nie pojawia. Tylko z narzędziem wyboru.",
    "Quando o cursor para sobre um edifício, lista até três perdas de eficiência que o atrapalham, com quanto custa cada uma. Os problemas já mostrados como ícones sobre o edifício não são repetidos, a menos que você tenha ocultado esses ícones. Se nada estiver errado, nada aparece. Apenas com a ferramenta de seleção.",
    "Когда курсор задерживается на здании, показывает до трёх потерь эффективности, которые ему мешают, и размер каждой. Проблемы, уже показанные значками над зданием, не повторяются, если вы не скрыли эти значки. Если всё в порядке, ничего не появляется. Только с инструментом выбора.",
    "光标停在建筑上时，列出最多三项拖累它的效率损失及各自的幅度。建筑上方已用图标显示的问题不会重复，除非你隐藏了这些图标。一切正常时不显示任何内容。仅在选择工具下。",
    "游標停在建築上時，列出最多三項拖累它的效率損失及各自的幅度。建築上方已用圖示顯示的問題不會重複，除非你隱藏了這些圖示。一切正常時不顯示任何內容。僅在選取工具下。")

add("option.label.ZoneTransparency",
    "Fade the zoning grid", "Bauraster abschwächen", "Atenuar la cuadrícula de zonificación",
    "Atténuer la grille de zonage", "Attenua la griglia di zonizzazione", "区画グリッドを薄く",
    "구획 격자 흐리게", "Przygaś siatkę stref", "Esmaecer a grade de zoneamento",
    "Приглушить сетку зон", "淡化分区网格", "淡化分區網格")
add("option.desc.ZoneTransparency",
    "Halves the opacity of the zoning cells drawn along the roads, so the ground shows through while you build. Off means the game looks exactly as it shipped. Disabled while Zone Color Changer is installed: both write the same colours, and the last one to write would silently win.",
    "Halbiert die Deckkraft der Bauzellen entlang der Straßen, sodass der Untergrund beim Bauen durchscheint. Aus bedeutet, dass das Spiel genau wie ausgeliefert aussieht. Deaktiviert, solange Zone Color Changer installiert ist: beide schreiben dieselben Farben, und der letzte Schreibvorgang würde unbemerkt gewinnen.",
    "Reduce a la mitad la opacidad de las celdas de zonificación junto a las carreteras, para que el terreno se vea al construir. Desactivado, el juego se ve tal y como se publicó. Se desactiva si Zone Color Changer está instalado: ambos escriben los mismos colores y el último en escribir ganaría sin avisar.",
    "Réduit de moitié l'opacité des cases de zonage le long des routes, pour voir le sol pendant la construction. Désactivé, le jeu a exactement son apparence d'origine. Désactivé tant que Zone Color Changer est installé : les deux écrivent les mêmes couleurs, et le dernier à écrire l'emporterait sans prévenir.",
    "Dimezza l'opacità delle celle di zonizzazione lungo le strade, così il terreno si vede mentre costruisci. Da spento il gioco appare esattamente come è stato pubblicato. Disattivata se è installato Zone Color Changer: entrambi scrivono gli stessi colori e l'ultimo a scrivere vincerebbe senza dirlo.",
    "道路沿いの区画セルの不透明度を半分にし、建設中も地面が透けて見えるようにします。オフなら発売時のままの見た目です。Zone Color Changer が導入されている間は無効です。どちらも同じ色を書き換えるため、後から書いた方が黙って勝ってしまいます。",
    "도로변 구획 칸의 불투명도를 절반으로 낮춰 건설 중에도 지면이 비쳐 보이게 합니다. 끄면 출시 당시 그대로의 모습입니다. Zone Color Changer가 설치된 동안에는 비활성화됩니다. 둘 다 같은 색을 쓰기 때문에 나중에 쓴 쪽이 조용히 이깁니다.",
    "Zmniejsza o połowę krycie komórek stref przy drogach, dzięki czemu podczas budowy widać teren. Wyłączone oznacza wygląd dokładnie taki, jak w wydanej grze. Nieaktywne, gdy zainstalowany jest Zone Color Changer: oba zapisują te same kolory, a ten, który zapisze później, wygrałby po cichu.",
    "Reduz à metade a opacidade das células de zoneamento ao longo das vias, para que o terreno apareça enquanto você constrói. Desligado, o jogo fica exatamente como foi lançado. Desativado enquanto o Zone Color Changer estiver instalado: ambos escrevem as mesmas cores, e o último a escrever venceria silenciosamente.",
    "Уменьшает непрозрачность ячеек зонирования вдоль дорог вдвое, чтобы во время строительства была видна земля. Выключено - игра выглядит ровно так, как вышла. Недоступно, пока установлен Zone Color Changer: оба записывают одни и те же цвета, и последний записавший победил бы незаметно.",
    "将道路两侧分区格子的不透明度减半，建造时可以看见地面。关闭时游戏外观与发行时完全一致。安装了 Zone Color Changer 时此项停用：两者写入同一组颜色，后写入的一方会悄悄覆盖另一方。",
    "將道路兩側分區格子的不透明度減半，建造時可以看見地面。關閉時遊戲外觀與發行時完全一致。安裝了 Zone Color Changer 時此項停用：兩者寫入同一組顏色，後寫入的一方會悄悄覆蓋另一方。")

io.open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "_stage1_ok"), "w").write("ok")
print("stage 1:", len(D), "chiavi")
