# -*- coding: utf-8 -*-
"""Everything the UI itself draws: headers, buttons, empty states, notes."""
from gen_locale import add

U = [
 # --- selected journey
 ("JOURNEY_SHOW", "Selected journey","Ausgewählte Fahrt","Viaje seleccionado","Trajet sélectionné","Viaggio selezionato","選択中の移動経路","선택한 이동 경로","Wybrana podróż","Viagem selecionada","Выбранная поездка","所选行程","所選行程"),
 ("WF_TIP_EDUCATION", "The highest education the citizen has finished.", "Der höchste Abschluss, den der Bürger erreicht hat.", "El nivel de estudios más alto que ha terminado el ciudadano.", "Le plus haut niveau d'études que le citoyen a terminé.", "Il titolo di studio più alto che il cittadino ha completato.", "市民が修了した最も高い教育レベル。", "시민이 마친 가장 높은 교육 수준.", "Najwyższy poziom wykształcenia, jaki ukończył mieszkaniec.", "O nível de estudo mais alto que o cidadão concluiu.", "Наивысший уровень образования, который житель завершил.", "市民完成的最高教育程度。", "市民完成的最高教育程度。"),
 ("WF_TIP_TOTAL", "Citizens who live here at this level. Tourists and commuters are not counted.", "Bürger mit diesem Abschluss, die hier wohnen. Touristen und Pendler zählen nicht.", "Ciudadanos que viven aquí con este nivel. No cuentan turistas ni quienes vienen de fuera a trabajar.", "Citoyens vivant ici à ce niveau. Touristes et navetteurs ne sont pas comptés.", "Cittadini residenti con questo livello. Turisti e pendolari esclusi.", "この教育レベルで市内に住む市民。観光客と通勤者は含みません。", "이 교육 수준으로 이곳에 사는 시민. 관광객과 통근자는 제외됩니다.", "Mieszkańcy z tym wykształceniem. Bez turystów i dojeżdżających.", "Cidadãos que moram aqui com este nível. Turistas e quem vem de fora trabalhar não contam.", "Жители с этим уровнем образования. Туристы и приезжающие на работу не учитываются.", "居住在本市、具有该教育程度的市民。不含游客和外来通勤者。", "居住在本市、具有該教育程度的市民。不含遊客和外來通勤者。"),
 ("WF_TIP_KIDS", "Children and teenagers who are not at school.", "Kinder und Jugendliche, die nicht zur Schule gehen.", "Niños y adolescentes que no estudian.", "Enfants et adolescents qui ne sont pas scolarisés.", "Bambini e ragazzi che non vanno a scuola.", "学校に通っていない子どもと若者。", "학교에 다니지 않는 어린이와 청소년.", "Dzieci i nastolatki, które nie chodzą do szkoły.", "Crianças e adolescentes que não estão na escola.", "Дети и подростки, которые не учатся.", "未上学的儿童和青少年。", "未上學的兒童和青少年。"),
 ("WF_TIP_STUDENT", "Citizens studying now, at any age.", "Bürger, die gerade lernen, in jedem Alter.", "Ciudadanos que estudian ahora, de cualquier edad.", "Citoyens qui étudient actuellement, à tout âge.", "Cittadini che stanno studiando, di qualsiasi età.", "現在学んでいる市民（年齢を問わず）。", "현재 공부 중인 시민(나이 무관).", "Mieszkańcy, którzy teraz się uczą, w każdym wieku.", "Cidadãos estudando agora, de qualquer idade.", "Жители, которые сейчас учатся, любого возраста.", "正在上学的市民，不限年龄。", "正在上學的市民，不限年齡。"),
 ("WF_TIP_OLD", "Seniors.", "Senioren.", "Personas mayores.", "Seniors.", "Anziani.", "高齢者。", "노인.", "Seniorzy.", "Idosos.", "Пожилые.", "老年人。", "老年人。"),
 ("WF_TIP_ADULTS", "Citizens of working age.", "Bürger im erwerbsfähigen Alter.", "Ciudadanos en edad de trabajar.", "Citoyens en âge de travailler.", "Cittadini in età lavorativa.", "生産年齢の市民。", "노동 연령의 시민.", "Mieszkańcy w wieku produkcyjnym.", "Cidadãos em idade de trabalhar.", "Жители трудоспособного возраста.", "处于工作年龄的市民。", "處於工作年齡的市民。"),
 ("WF_TIP_EMPLOYED", "Working at a job in the city.", "Arbeiten in einem Betrieb in der Stadt.", "Trabajan en un empleo de la ciudad.", "Travaillent dans un emploi de la ville.", "Lavorano in un posto di lavoro in città.", "市内の職場で働いている人。", "도시 안 일자리에서 일하는 사람.", "Pracują w mieście.", "Trabalham em um emprego na cidade.", "Работают в городе.", "在本市工作的人。", "在本市工作的人。"),
 ("WF_TIP_UNEMPLOYED", "Able to work but without a job in the city, as the game counts them.", "Arbeitsfähig, aber ohne Arbeit in der Stadt, so wie das Spiel zählt.", "Pueden trabajar pero no tienen empleo en la ciudad, como lo cuenta el juego.", "En mesure de travailler mais sans emploi dans la ville, comme le compte le jeu.", "Potrebbero lavorare ma non hanno un lavoro in città, come li conta il gioco.", "働けるが市内に職がない人（ゲームの集計どおり）。", "일할 수 있지만 도시 안에 일자리가 없는 사람(게임 기준).", "Mogą pracować, ale nie mają pracy w mieście, tak jak liczy gra.", "Podem trabalhar, mas não têm emprego na cidade, como o jogo conta.", "Могут работать, но не имеют работы в городе, как считает игра.", "有工作能力但在本市没有工作的人（按游戏统计）。", "有工作能力但在本市沒有工作的人（依遊戲統計）。"),
 ("WF_TIP_UNDER", "Employed in a job below their education.", "Arbeiten unter ihrer Qualifikation.", "Trabajan por debajo de su formación.", "Travaillent en dessous de leur qualification.", "Lavorano sotto il proprio titolo di studio.", "学歴より低い仕事に就いている人。", "학력보다 낮은 일을 하는 사람.", "Pracują poniżej swoich kwalifikacji.", "Trabalham abaixo da sua formação.", "Работают ниже своей квалификации.", "从事低于其学历工作的人。", "從事低於其學歷工作的人。"),
 ("WF_TIP_OUT", "Live here and work outside the city.", "Wohnen hier und arbeiten außerhalb der Stadt.", "Viven aquí y trabajan fuera de la ciudad.", "Vivent ici et travaillent hors de la ville.", "Vivono qui e lavorano fuori città.", "市内に住み、市外で働く人。", "이곳에 살고 도시 밖에서 일하는 사람.", "Mieszkają tu i pracują poza miastem.", "Moram aqui e trabalham fora da cidade.", "Живут здесь и работают за городом.", "住在本市、在市外工作的人。", "住在本市、在市外工作的人。"),
 ("WF_TIP_IN", "Work here and live outside the city.", "Arbeiten hier und wohnen außerhalb der Stadt.", "Trabajan aquí y viven fuera de la ciudad.", "Travaillent ici et vivent hors de la ville.", "Lavorano qui e vivono fuori città.", "市内で働き、市外に住む人。", "이곳에서 일하고 도시 밖에 사는 사람.", "Pracują tu i mieszkają poza miastem.", "Trabalham aqui e moram fora da cidade.", "Работают здесь и живут за городом.", "在本市工作、住在市外的人。", "在本市工作、住在市外的人。"),
 ("WF_TIP_JOBS", "Jobs at this level in the city's buildings.", "Arbeitsplätze dieser Stufe in den Gebäuden der Stadt.", "Empleos de este nivel en los edificios de la ciudad.", "Emplois de ce niveau dans les bâtiments de la ville.", "Posti di lavoro di questo livello negli edifici della città.", "市内の建物にあるこのレベルの仕事。", "도시 건물에 있는 이 수준의 일자리.", "Miejsca pracy na tym poziomie w budynkach miasta.", "Empregos deste nível nos prédios da cidade.", "Рабочие места этого уровня в зданиях города.", "本市建筑中该教育程度的职位。", "本市建築中該教育程度的職位。"),
 ("WF_TIP_VACANT", "Of those jobs, the ones nobody is doing. Orange when they outnumber the adults at this level.", "Davon die Stellen, die niemand besetzt. Orange, wenn es mehr sind als Erwachsene dieser Stufe.", "De esos empleos, los que nadie ocupa. En naranja si superan a los adultos de este nivel.", "Parmi ces emplois, ceux que personne n'occupe. En orange s'ils dépassent les adultes de ce niveau.", "Di quei posti, quelli che nessuno occupa. In arancione quando superano gli adulti di questo livello.", "そのうち誰も就いていない仕事。このレベルの大人より多いとオレンジ色になります。", "그중 아무도 하지 않는 일자리. 이 수준의 성인보다 많으면 주황색입니다.", "Z tych miejsc te, których nikt nie zajmuje. Pomarańczowe, gdy jest ich więcej niż dorosłych na tym poziomie.", "Desses empregos, os que ninguém ocupa. Em laranja quando superam os adultos deste nível.", "Из них те, которые никто не занимает. Оранжевые, если их больше, чем взрослых этого уровня.", "其中无人担任的职位。多于该程度的成年人时显示为橙色。", "其中無人擔任的職位。多於該程度的成年人時顯示為橙色。"),
 ("STOPS_OPEN_STOP", "Open stop","Haltestelle öffnen","Abrir parada","Ouvrir l'arrêt","Apri fermata","停留所を開く","정류장 열기","Otwórz przystanek","Abrir parada","Открыть остановку","打开站点","開啟站點"),
 ("STREETNAMES_ALL", "Street names: all shown","Straßennamen: alle sichtbar","Nombres de calles: todos","Noms de rues : tous affichés","Nomi delle strade: tutti visibili","通りの名前：すべて表示","도로 이름: 전부 표시","Nazwy ulic: wszystkie widoczne","Nomes das ruas: todos visíveis","Названия улиц: все видны","街道名称：全部显示","街道名稱：全部顯示"),
 ("STREETNAMES_CUSTOM", "Street names: only the ones you renamed","Straßennamen: nur selbst vergebene","Nombres de calles: solo los que renombraste","Noms de rues : seulement ceux que vous avez renommés","Nomi delle strade: solo quelli rinominati da te","通りの名前：自分で名付けたものだけ","도로 이름: 직접 바꾼 이름만","Nazwy ulic: tylko zmienione przez ciebie","Nomes das ruas: só os que você renomeou","Названия улиц: только переименованные вами","街道名称：仅你重命名的","街道名稱：僅你重新命名的"),
 ("STREETNAMES_NONE", "Street names: hidden","Straßennamen: ausgeblendet","Nombres de calles: ocultos","Noms de rues : masqués","Nomi delle strade: nascosti","通りの名前：非表示","도로 이름: 숨김","Nazwy ulic: ukryte","Nomes das ruas: ocultos","Названия улиц: скрыты","街道名称：隐藏","街道名稱：隱藏"),
 ("STREETNAMES_CLICK", "Click to change","Klicken zum Wechseln","Haz clic para cambiar","Cliquez pour changer","Clicca per cambiare","クリックで切り替え","클릭하여 변경","Kliknij, aby zmienić","Clique para mudar","Нажмите, чтобы изменить","点击切换","點擊切換"),
 ("JOURNEY_EMPTY", "Select a citizen or vehicle with an active journey.","Wähle einen Bürger oder ein Fahrzeug mit einer laufenden Fahrt.","Selecciona un ciudadano o vehículo que esté viajando.","Sélectionnez un citoyen ou un véhicule en déplacement.","Seleziona un cittadino o un veicolo con un viaggio in corso.","移動中の市民または車両を選択してください。","이동 중인 시민이나 차량을 선택하세요.","Wybierz mieszkańca lub pojazd będący w podróży.","Selecione um cidadão ou veículo em viagem.","Выберите жителя или транспортное средство в пути.","选择正在出行的市民或车辆。","選擇正在出行的市民或車輛。"),
 ("JOURNEY_HERE", "Now","Aktuell","Ahora","Actuellement","Adesso","現在地","현재 위치","Teraz","Agora","Сейчас","当前位置","目前位置"),
 ("JOURNEY_DESTINATION", "Destination","Ziel","Destino","Destination","Destinazione","目的地","목적지","Cel","Destino","Назначение","目的地","目的地"),
 ("JOURNEY_UNKNOWN", "Unavailable","Nicht verfügbar","No disponible","Indisponible","Non disponibile","不明","정보 없음","Brak danych","Indisponível","Нет данных","暂无信息","暫無資訊"),
 ("JOURNEY_REMAINING", "Remaining journey","Verbleibender Weg","Trayecto restante","Trajet restant","Percorso rimanente","残りの経路","남은 경로","Pozostała trasa","Trajeto restante","Оставшийся путь","剩余行程","剩餘行程"),
 ("JOURNEY_NO_PATH", "No remaining route is available.","Kein verbleibender Weg verfügbar.","No hay información del trayecto restante.","Aucun trajet restant disponible.","Nessun percorso rimanente disponibile.","残りの経路はありません。","남은 경로 정보가 없습니다.","Brak informacji o pozostałej trasie.","Nenhum trajeto restante disponível.","Нет данных об оставшемся пути.","暂无剩余路线信息。","暫無剩餘路線資訊。"),
 ("JOURNEY_OPEN_LINE", "Open transport line","Verkehrslinie öffnen","Abrir línea de transporte","Ouvrir la ligne de transport","Apri linea di trasporto","交通路線を開く","교통 노선 열기","Otwórz linię transportową","Abrir linha de transporte","Открыть транспортную линию","打开交通线路","開啟交通路線"),
 ("JOURNEY_GO", "Go to the destination","Zum Ziel springen","Ir al destino","Aller à la destination","Vai alla destinazione","目的地へ移動","목적지로 이동","Przejdź do celu","Ir para o destino","Перейти к месту назначения","前往目的地","前往目的地"),
 ("JOURNEY_TRUNCATED", "Only the first part of this journey is shown.","Nur der erste Teil dieses Weges wird angezeigt.","Solo se muestra la primera parte del trayecto.","Seule la première partie du trajet est affichée.","È mostrata solo la prima parte del percorso.","経路の最初の部分のみ表示しています。","경로의 첫 부분만 표시됩니다.","Wyświetlana jest tylko pierwsza część trasy.","Apenas a primeira parte do trajeto é exibida.","Показана только первая часть пути.","仅显示此行程的前半部分。","僅顯示此行程的前段。"),
 ("TRAFFIC_TRANSIT", "Public transport","Nahverkehr","Transporte público","Transports en commun","Trasporto pubblico","公共交通","대중교통","Transport publiczny","Transporte público","Общественный транспорт","公共交通","大眾運輸"),
 ("TRAFFIC_ROAD", "Road traffic","Straßenverkehr","Tráfico rodado","Trafic routier","Traffico stradale","道路交通","도로 교통","Ruch drogowy","Tráfego rodoviário","Дорожное движение","道路交通","道路交通"),
 # --- strip chrome
 ("ICONS_HIDE_TIP", "Hide the notification icons over the city","Benachrichtigungssymbole über der Stadt ausblenden","Ocultar los iconos de notificación sobre la ciudad","Masquer les icônes de notification au-dessus de la ville","Nascondi le icone di notifica sopra la città","街の上の通知アイコンを隠します","도시 위의 알림 아이콘을 숨깁니다","Ukryj ikony powiadomień nad miastem","Ocultar os ícones de notificação sobre a cidade","Скрыть значки уведомлений над городом","隐藏城市上方的通知图标","隱藏城市上方的通知圖示"),
 ("ICONS_SHOW_TIP", "Show the notification icons over the city","Benachrichtigungssymbole über der Stadt einblenden","Mostrar los iconos de notificación sobre la ciudad","Afficher les icônes de notification au-dessus de la ville","Mostra le icone di notifica sopra la città","街の上の通知アイコンを表示します","도시 위의 알림 아이콘을 표시합니다","Pokaż ikony powiadomień nad miastem","Mostrar os ícones de notificação sobre a cidade","Показать значки уведомлений над городом","显示城市上方的通知图标","顯示城市上方的通知圖示"),
 # --- empty states
 ("EMPTY_NOTHING", "Nothing to report","Nichts zu melden","Nada que informar","Rien à signaler","Nulla da segnalare","報告なし","보고할 내용 없음","Nic do zgłoszenia","Nada a relatar","Сообщать нечего","无内容","無內容"),
 ("EMPTY_FACTORS", "No factors reported","Keine Faktoren gemeldet","No se informan factores","Aucun facteur signalé","Nessun fattore segnalato","要因の報告なし","보고된 요인 없음","Brak zgłoszonych czynników","Nenhum fator relatado","Факторы не сообщаются","无相关因素","無相關因素"),
 ("EMPTY_TRADE", "Nothing traded yet","Noch kein Handel","Aún no hay comercio","Aucun échange pour l'instant","Ancora nessuno scambio","取引はまだありません","아직 거래 없음","Brak handlu","Ainda sem comércio","Торговли пока нет","尚无贸易","尚無貿易"),
 # --- tooltips
 ("TIP_FACTOR", "{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT} %","{FACTOR}: -{PERCENT} %","{FACTOR} : -{PERCENT} %","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}: -{PERCENT}%","{FACTOR}：-{PERCENT}%","{FACTOR}：-{PERCENT}%"),
 ("TIP_GO_THERE", "click to go there","klicken, um hinzuspringen","haz clic para ir allí","cliquez pour y aller","clicca per andarci","クリックでその場所へ","클릭하면 그곳으로 이동","kliknij, aby tam przejść","clique para ir até lá","нажмите, чтобы перейти","点击前往","點擊前往"),
 ("TIP_OPEN_INFO", "click to open its info view","klicken, um die Infoansicht zu öffnen","haz clic para abrir su vista de información","cliquez pour ouvrir la vue d'information","clicca per aprire la vista informativa","クリックでインフォビューを開く","클릭하면 정보 보기를 엽니다","kliknij, aby otworzyć widok informacji","clique para abrir a visão de informação","нажмите, чтобы открыть режим информации","点击打开信息视图","點擊開啟資訊檢視"),
 ("TIP_SELECT_MODE", "click to select this mode in the transport overview","klicken, um diesen Verkehrsträger in der Übersicht zu wählen","haz clic para seleccionar este modo en el resumen de transporte","cliquez pour sélectionner ce mode dans l'aperçu des transports","clicca per selezionare questo mezzo nel riepilogo trasporti","クリックで交通概要のこの手段を選択","클릭하면 교통 개요에서 이 수단을 선택합니다","kliknij, aby wybrać ten środek w przeglądzie transportu","clique para selecionar este modo na visão geral de transporte","нажмите, чтобы выбрать этот вид в обзоре транспорта","点击在交通总览中选择该方式","點擊在交通總覽中選擇該方式"),
 # --- transport window
 ("TRANSPORT_STOPS", "Busiest stops", "Meistbesuchte Haltestellen", "Paradas más concurridas", "Arrêts les plus fréquentés", "Fermate più frequentate", "混雑している停留所", "가장 붐비는 정류장", "Najbardziej oblegane przystanki", "Paradas mais movimentadas", "Самые загруженные остановки", "最繁忙的站点", "最繁忙的站點"),
 ("TRANSPORT_MODES", "By transport mode", "Nach Verkehrsmittel", "Por medio de transporte", "Par mode de transport", "Per mezzo di trasporto", "交通手段別", "교통수단별", "Według środka transportu", "Por meio de transporte", "По виду транспорта", "按交通方式", "依交通方式"),
 ("STOPS_REFRESH", "Refresh", "Aktualisieren", "Actualizar", "Actualiser", "Aggiorna", "更新", "새로 고침", "Odśwież", "Atualizar", "Обновить", "刷新", "重新整理"),
 ("STOPS_EMPTY", "No passengers waiting at stops.", "Keine Fahrgäste warten an Haltestellen.", "No hay pasajeros esperando en las paradas.", "Aucun passager en attente aux arrêts.", "Nessun passeggero in attesa alle fermate.", "停留所で待っている乗客はいません。", "정류장에서 기다리는 승객이 없습니다.", "Na przystankach nie czekają pasażerowie.", "Nenhum passageiro esperando nas paradas.", "На остановках нет ожидающих пассажиров.", "站点没有等候的乘客。", "站點沒有等候的乘客。"),
 ("STOPS_UNNAMED", "Unnamed stop", "Unbenannte Haltestelle", "Parada sin nombre", "Arrêt sans nom", "Fermata senza nome", "名称未設定の停留所", "이름 없는 정류장", "Przystanek bez nazwy", "Parada sem nome", "Остановка без названия", "未命名站点", "未命名站點"),
 ("STOPS_WAITING", "waiting", "warten", "en espera", "en attente", "in attesa", "待機中", "대기 중", "czeka", "em espera", "ожидают", "等候中", "等候中"),
 ("SEC_PASSENGERS", "Passengers","Fahrgäste","Pasajeros","Passagers","Passeggeri","旅客","승객","Pasażerowie","Passageiros","Пассажиры","客运","客運"),
 ("SEC_CARGO", "Cargo","Fracht","Carga","Fret","Merci","貨物","화물","Ładunek","Carga","Грузы","货运","貨運"),
 # --- workforce table
 ("WF_EDUCATION", "Education","Bildung","Educación","Éducation","Istruzione","学歴","학력","Wykształcenie","Educação","Образование","教育","教育"),
 ("WF_TOTAL", "Total","Gesamt","Total","Total","Totale","合計","합계","Razem","Total","Всего","合计","合計"),
 ("WF_KIDS", "Kids","Kinder","Niños","Enfants","Bambini","子供","아동","Dzieci","Crianças","Дети","儿童","兒童"),
 ("WF_STUDENT", "Student","Schüler","Estudia","Élèves","Studenti","学生","학생","Uczniowie","Estudam","Учатся","在学","在學"),
 ("WF_OLD", "Old","Senioren","Mayores","Aînés","Anziani","高齢","고령","Seniorzy","Idosos","Пожилые","老年","老年"),
 ("WF_ADULTS", "Adults","Erwachsene","Adultos","Adultes","Adulti","成人","성인","Dorośli","Adultos","Взрослые","成人","成人"),
 ("WF_EMPLOYED", "Employed","Beschäftigt","Empleados","Employés","Occupati","就業","취업","Zatrudnieni","Empregados","Заняты","就业","就業"),
 ("WF_UNEMPLOYED", "Unemployed","Arbeitslos","Desempleados","Sans emploi","Disoccupati","失業","실업","Bezrobotni","Desempregados","Безработные","失业","失業"),
 ("WF_UNDER", "Under","Unterqualifiziert","Subempleo","Sous-qualifié","Sottoimpiego","過小就業","하향취업","Poniżej","Subemprego","Ниже уровня","低配","低配"),
 ("WF_OUT", "Out","Auswärts","Fuera","Dehors","Fuori","市外勤務","외부근무","Poza","Fora","Вне города","外出","外出"),
 ("WF_IN", "In","Einpendler","Entran","Entrants","Entranti","流入","유입","Dojazd","Entram","Въезд","流入","流入"),
 ("WF_JOBS", "Jobs","Stellen","Puestos","Postes","Posti","職","일자리","Posady","Postos","Места","岗位","職位"),
 ("WF_VACANT", "Vacant","Unbesetzt","Vacantes","Vacants","Liberi","空き","공석","Wolne","Vagos","Вакансии","空缺","空缺"),
 # --- demographics
 ("DEMO_AGE", "Age","Alter","Edad","Âge","Età","年齢","연령","Wiek","Idade","Возраст","年龄","年齡"),
 ("AGE_CHILDREN", "Children","Kinder","Niños","Enfants","Bambini","子供","아동","Dzieci","Crianças","Дети","儿童","兒童"),
 ("AGE_TEENS", "Teens","Jugendliche","Adolescentes","Adolescents","Adolescenti","10代","청소년","Nastolatki","Adolescentes","Подростки","青少年","青少年"),
 ("AGE_ADULTS", "Adults","Erwachsene","Adultos","Adultes","Adulti","成人","성인","Dorośli","Adultos","Взрослые","成人","成人"),
 ("AGE_SENIORS", "Seniors","Senioren","Mayores","Aînés","Anziani","高齢者","노년","Seniorzy","Idosos","Пожилые","老年人","老年人"),
 ("EDU_NONE", "None","Ohne","Ninguna","Aucune","Nessuna","なし","없음","Brak","Nenhuma","Нет","无","無"),
 ("EDU_POOR", "Poor","Gering","Baja","Faible","Bassa","低","낮음","Niskie","Baixa","Низкое","低","低"),
 ("EDU_EDUCATED", "Educated","Mittel","Media","Moyenne","Media","中","보통","Średnie","Média","Среднее","中","中"),
 ("EDU_WELL", "Well","Gut","Alta","Bonne","Buona","高","높음","Wyższe","Alta","Высокое","高","高"),
 ("EDU_HIGHLY", "Highly","Sehr gut","Muy alta","Très bonne","Ottima","最高","최고","Najwyższe","Muito alta","Высшее","最高","最高"),
 # --- resource tables
 ("RES_RESOURCE", "Resource","Ware","Recurso","Ressource","Risorsa","資源","자원","Towar","Recurso","Ресурс","资源","資源"),
 ("RES_WANTED", "Wanted","Gefragt","Demandado","Demandé","Richiesta","需要度","수요","Popyt","Procurado","Спрос","需求","需求"),
 ("RES_STAFF", "Staff","Personal","Personal","Personnel","Personale","人員","인력","Obsada","Pessoal","Персонал","人手","人手"),
 ("RES_SHOPS", "Shops","Geschäfte","Tiendas","Commerces","Negozi","店舗","상점","Sklepy","Lojas","Магазины","商店","商店"),
 ("RES_PLANTS", "Plants","Betriebe","Plantas","Usines","Impianti","工場","공장","Zakłady","Fábricas","Заводы","工厂","工廠"),
 ("RES_OFFICES", "Offices","Büros","Oficinas","Bureaux","Uffici","事業所","사무실","Biura","Escritórios","Офисы","办公","辦公"),
 ("RES_STOCK", "Stock","Bestand","Existencias","Stock","Scorte","在庫","재고","Zapas","Estoque","Запас","库存","庫存"),
 ("RES_MADE", "Made","Produziert","Producido","Produit","Prodotto","生産","생산","Produkcja","Produzido","Произведено","产量","產量"),
 # --- parking
 ("PARK_CARS", "Cars","Autos","Coches","Voitures","Auto","自動車","자동차","Samochody","Carros","Автомобили","汽车","汽車"),
 ("PARK_BIKES", "Bikes","Fahrräder","Bicicletas","Vélos","Bici","自転車","자전거","Rowery","Bicicletas","Велосипеды","自行车","自行車"),
 # --- demand rows
 ("ZONE_RES_LOW", "Residential low","Wohnen (niedrig)","Residencial baja","Résidentiel faible","Residenziale bassa","住宅（低密度）","주거 저밀도","Mieszkalna niska","Residencial baixa","Жилая (низкая)","低密度住宅","低密度住宅"),
 ("ZONE_RES_MED", "Residential medium","Wohnen (mittel)","Residencial media","Résidentiel moyen","Residenziale media","住宅（中密度）","주거 중밀도","Mieszkalna średnia","Residencial média","Жилая (средняя)","中密度住宅","中密度住宅"),
 ("ZONE_RES_HIGH", "Residential high","Wohnen (hoch)","Residencial alta","Résidentiel élevé","Residenziale alta","住宅（高密度）","주거 고밀도","Mieszkalna wysoka","Residencial alta","Жилая (высокая)","高密度住宅","高密度住宅"),
 ("ZONE_COMMERCIAL", "Commercial","Gewerbe","Comercial","Commercial","Commerciale","商業","상업","Handlowa","Comercial","Коммерческая","商业","商業"),
 ("ZONE_INDUSTRIAL", "Industrial","Industrie","Industrial","Industriel","Industriale","工業","공업","Przemysłowa","Industrial","Промышленная","工业","工業"),
 ("ZONE_OFFICE", "Office","Büro","Oficinas","Bureaux","Uffici","オフィス","사무","Biurowa","Escritórios","Офисная","办公","辦公"),
 ("PARKED_OF", "parked of","belegt von","ocupadas de","occupées sur","occupati su","／","／","zajęte z","ocupadas de","занято из","已占用／共","已佔用／共"),
]

for row in U:
    add("Seety." + row[0], *row[1:])

# Education balance was first prototyped in English and Italian. Keep the shared keys in the
# generator; the other locales use English until their translations are reviewed.
for key, en, it in [
    ("EB_TITLE", "Education balance", "Bilancio dell'istruzione"),
    ("EB_RESUME", "Resume", "Riprendi"),
    ("EB_PAUSE", "Pause", "Pausa"),
    ("EB_CLEAR", "Clear history", "Cancella lo storico"),
    ("EB_WAIT", "Waiting for the next sample.", "In attesa del prossimo campione."),
    ("EB_PAUSED", "Observation paused.", "Osservazione in pausa."),
    ("EB_GAME_DAYS", "game days", "giorni di gioco"),
    ("EB_GAME_DAY", "game day", "giorno di gioco"),
    ("EB_FIRST", "First seen at this level", "Osservati per la prima volta a questo livello"),
    ("EB_CHANGED_IN", "Changed into this level", "Passati a questo livello"),
    ("EB_CHANGED_OUT", "Changed out of this level", "Passati a un altro livello"),
    ("EB_GONE", "No longer observed", "Non più osservati"),
    ("EB_TIP_FIRST", "Not necessarily births or arrivals.", "Non necessariamente nascite o arrivi."),
    ("EB_TIP_GONE", "Not necessarily deaths or departures.", "Non necessariamente decessi o partenze."),
    ("RES_TIP_WANTED", "Compared with the most wanted good.", "Rispetto al bene più richiesto."),
    ("EB_FROM", "Observed transitions from", "Passaggi osservati da"),
    ("EB_TO", "Observed transitions to", "Passaggi osservati a"),
    ("EB_GAP", "Unreconciled change", "Variazione non riconciliata"),
    ("EB_GAP_SHORT", "gap before", "intervallo non osservato prima"),
    ("EB_COVERAGE_GAP", "Change across observation gaps", "Variazione negli intervalli non osservati"),
    ("EB_TABLE_TAB", "Workforce table", "Tabella dei lavoratori"),
    ("EB_BALANCE_TAB", "Education balance", "Bilancio dell'istruzione"),
]:
    values = [en] * 12
    values[4] = it
    add("Seety." + key, *values)

for key, en, it in [
    ("UNEMPLOYMENT_TOTAL", "Total", "Totale"),
    ("UNEMPLOYMENT_BY_EDUCATION", "By education", "Per istruzione"),
    ("UNEMPLOYMENT_SAMPLED", "Observed", "Osservati"),
    ("UNEMPLOYMENT_WAIT", "Waiting for the second sample.", "In attesa del secondo campione."),
    ("UNEMPLOYMENT_PAUSED", "Observation paused.", "Osservazione in pausa."),
]:
    values = [en] * 12
    values[4] = it
    add("Seety." + key, *values)


print("ui:", len(U))
