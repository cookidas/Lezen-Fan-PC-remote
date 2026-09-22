namespace LezenTray;

/// <summary>
/// Minimal localization table. Default language is English ("en"); Korean ("ko") is
/// selectable from the tray "Language" menu. Not a .resx setup on purpose — this app's
/// string set is small enough that a plain dictionary is easier to keep in sync.
/// </summary>
public static class Strings
{
    public const string DefaultLanguage = "en";
    public static string Language { get; set; } = DefaultLanguage;
    public static bool IsKorean => Language == "ko";
    public static string FontFamily => IsKorean ? "맑은 고딕" : "Segoe UI";

    private static readonly Dictionary<string, (string En, string Ko)> Map = new()
    {
        // Tray icon / menu title
        ["tray.text"] = ("LEZEN Fan", "LEZEN 선풍기"),
        ["menu.title.none"] = ("LEZEN · No fan selected", "LEZEN · 선택한 선풍기 없음"),
        ["menu.title.device"] = ("LEZEN · {0}", "LEZEN · {0}"),
        ["tooltip.add_fan"] = ("LEZEN · Add a fan", "LEZEN · 선풍기를 추가하세요"),
        ["tooltip.device"] = ("LEZEN · {0}", "LEZEN · {0}"),

        // Power / swing
        ["power.on_to_off"] = ("Power · On → Turn off", "전원 · 켜짐 → 끄기"),
        ["power.off_to_on"] = ("Power · Off → Turn on", "전원 · 꺼짐 → 켜기"),
        ["power.unknown_to_on"] = ("Power · Unknown → Turn on", "전원 · 미확인 → 켜기"),
        ["swing.on_to_off"] = ("Swing · On → Turn off", "좌우 회전 · 켜짐 → 끄기"),
        ["swing.off_to_on"] = ("Swing · Off → Turn on", "좌우 회전 · 꺼짐 → 켜기"),
        ["swing.unknown_to_on"] = ("Swing · Unknown → Turn on", "좌우 회전 · 미확인 → 켜기"),

        // Speed / timer / mode submenus
        ["menu.speed"] = ("Wind speed", "바람 강도"),
        ["menu.speed_up"] = ("＋ Stronger", "＋ 바람 세게"),
        ["menu.speed_down"] = ("－ Weaker", "－ 바람 약하게"),
        ["menu.timer"] = ("Timer", "타이머"),
        ["menu.timer_up"] = ("＋ Add time", "＋ 시간 늘리기"),
        ["menu.timer_down"] = ("－ Reduce time", "－ 시간 줄이기"),
        ["menu.mode"] = ("Wind mode", "바람 모드"),
        ["mode.normal"] = ("Normal", "일반풍"),
        ["mode.natural"] = ("Natural", "자연풍"),
        ["mode.sleep"] = ("Sleep", "수면풍"),
        ["mode.temperature"] = ("Temperature", "온도모드"),

        // Devices submenu
        ["menu.devices"] = ("Add fan", "선풍기 추가"),
        ["menu.devices.add_mode"] = ("Add device mode…", "장치 추가 모드…"),
        ["menu.devices.none"] = ("No devices added", "추가된 장치 없음"),
        ["menu.devices.manage"] = ("Manage devices…", "장치 관리…"),
        ["device.tooltip"] = ("Device ID: {0}", "장치 ID: {0}"),
        ["err.device_select_save"] = ("Couldn't save device selection", "장치 선택 저장 실패"),

        // Language submenu
        ["menu.language"] = ("Language", "언어"),
        ["lang.english"] = ("English", "English"),
        ["lang.korean"] = ("한국어", "한국어"),

        // Status / log / exit
        ["status.default"] = ("Status shown as of last command", "상태 표시는 마지막 전송 기준"),
        ["status.sending"] = ("Sending signal…", "신호 전송 중…"),
        ["status.send_failed"] = ("Send failed · status reset", "전송 실패 · 표시 상태 초기화됨"),
        ["title.send_failed"] = ("LEZEN signal send failed", "LEZEN 신호 전송 실패"),
        ["menu.log"] = ("Open diagnostic log", "진단 로그 열기"),
        ["title.log"] = ("Diagnostic log", "진단 로그"),
        ["menu.exit"] = ("Exit", "종료"),

        // Device management form
        ["form.title"] = ("LEZEN · Add and manage fans", "LEZEN · 선풍기 추가 및 관리"),
        ["accessible.saved_list"] = ("Added fans", "추가된 선풍기"),
        ["accessible.name_input"] = ("Fan name to add", "추가할 선풍기 이름"),
        ["accessible.id_input"] = ("Four-character fan ID", "선풍기 식별 코드 네 자리"),
        ["accessible.saved_name"] = ("Selected fan name", "선택한 선풍기 이름"),
        ["btn.bind"] = ("Send registration signal", "등록 신호 보내기"),
        ["btn.new_id"] = ("New code", "새 코드"),
        ["btn.resend"] = ("Resend registration signal", "등록 신호 다시 보내기"),
        ["btn.select"] = ("Select and close", "선택하고 닫기"),
        ["btn.rename"] = ("Rename", "이름 변경"),
        ["btn.remove"] = ("Remove from list", "목록에서 제거"),
        ["btn.close"] = ("Close", "닫기"),
        ["intro.text"] = (
            "Unplug the fan, plug it back in, and send the registration signal within 10 minutes.\nAfter registering, check the tray's power button to see if the fan reacts.",
            "선풍기 전원 플러그를 뺐다가 다시 꽂고, 10분 안에 등록 신호를 보내세요.\n등록 후 트레이의 전원 버튼을 눌러 선풍기가 반응하는지 확인하세요."),
        ["group.add"] = ("Add a fan", "선풍기 추가"),
        ["field.name"] = ("Name", "이름"),
        ["field.id"] = ("ID code", "식별 코드"),
        ["hint.id"] = (
            "A four-character code using 0–9, A–F. You can just use the auto-generated code.\nIf you already know the code from the phone app, you can enter that same code.",
            "0–9, A–F로 된 네 자리 코드입니다. 자동 생성한 코드를 그대로 쓸 수 있어요.\n스마트폰 앱의 기존 코드를 알고 있다면 같은 코드를 입력할 수 있습니다."),
        ["group.saved"] = ("Added fans", "추가된 선풍기"),
        ["col.name"] = ("Fan name", "선풍기 이름"),
        ["col.id"] = ("ID code", "식별 코드"),
        ["col.inuse"] = ("In use", "사용 중"),
        ["col.selected"] = ("Selected", "선택됨"),
        ["status.closing_wait"] = ("Finishing the registration signal before closing…", "등록 신호 전송을 마친 뒤 닫을 수 있습니다…"),
        ["status.initial"] = (
            "Whether the signal was received can only be confirmed by the fan's reaction.",
            "등록 신호의 수신 여부는 선풍기 반응으로 확인해야 합니다."),
        ["status.need_name"] = ("Enter a fan name.", "선풍기 이름을 입력하세요."),
        ["status.bad_id"] = ("The ID code must be four characters using 0–9, A–F.", "식별 코드는 0–9, A–F로 된 네 자리여야 합니다."),
        ["status.dup_id"] = (
            "This code is already added. Use \u2018Resend registration signal\u2019 on the selected device instead.",
            "이미 추가한 코드입니다. 선택한 장치의 \u2018등록 신호 다시 보내기\u2019를 사용하세요."),
        ["status.sending_named"] = ("{0} · Sending registration signal…", "{0} · 등록 신호를 전송하고 있습니다…"),
        ["status.save_failed"] = (
            "Sent the registration signal, but couldn't save it to the list. Keep code {0} and try again.",
            "등록 신호는 전송했지만 목록을 저장하지 못했습니다. 코드 {0}를 보관하고 다시 시도하세요."),
        ["status.sent"] = ("Registration signal sent. Check the power button to see if it reacts.", "등록 신호 전송 완료. 전원 버튼으로 반응을 확인하세요."),
        ["title.registration_failed"] = ("Registration signal failed", "등록 신호 전송 실패"),
        ["status.renamed"] = ("Fan name changed.", "선풍기 이름을 변경했습니다."),
        ["status.removed"] = (
            "Removed from the list. This does not reset the code stored on the fan.",
            "목록에서 제거했습니다. 선풍기에 저장된 코드는 초기화하지 않습니다."),
        ["status.list_save_failed"] = ("Couldn't save the device list.\n{0}", "장치 목록을 저장하지 못했습니다.\n{0}"),
        ["title.lezen"] = ("LEZEN", "LEZEN"),

        // AppSettings
        ["err.settings_unreadable"] = ("Couldn't read the device settings.", "장치 설정을 읽을 수 없습니다."),
        ["err.settings_invalid"] = ("The device settings are invalid. The existing file was kept.", "장치 설정이 올바르지 않습니다. 기존 파일은 보존됩니다."),

        // FanProtocol
        ["err.bad_id_arg"] = ("The device ID must be four characters using 0–9, A–F.", "장치 ID는 0~9, A~F로 된 네 자리여야 합니다."),

        // AdvertisementSender
        ["err.no_adapter"] = ("No Bluetooth adapter", "블루투스 어댑터 없음"),
        ["adapter.describe"] = (
            "{0} · Power {1} · BLE {2} · Peripheral role {3} · Advertising offload {4}",
            "{0} · 전원 {1} · BLE {2} · 주변 장치 역할 {3} · 광고 오프로드 {4}"),
        ["radio.state_unknown"] = ("unavailable", "확인 불가"),
        ["err.payload_length"] = ("The advertisement command data must be 1–24 bytes.", "광고 명령 데이터는 1~24바이트여야 합니다."),
        ["err.prev_send_unfinished"] = (
            "Couldn't confirm the previous Bluetooth transmission finished. Restart the app.",
            "이전 블루투스 송신 종료를 확인하지 못했습니다. 앱을 다시 실행하세요."),
        ["err.no_ble_adapter"] = ("No BLE adapter available.", "사용 가능한 BLE 어댑터가 없습니다."),
        ["err.bluetooth_off"] = ("Please turn on Bluetooth in Windows settings.", "윈도우 설정에서 블루투스를 켜 주세요."),
        ["err.advert_aborted"] = ("The Bluetooth advertisement was aborted. Status: Aborted", "블루투스 광고가 중단되었습니다. 상태: Aborted"),
        ["err.send_unconfirmed"] = (
            "Couldn't confirm the Bluetooth transmission finished. Restart the app.",
            "블루투스 송신 종료를 확인하지 못했습니다. 앱을 다시 실행하세요."),
        ["err.send_start_failed"] = (
            "Couldn't start the Bluetooth advertisement. Check the adapter and driver.",
            "블루투스 광고 송신을 시작하지 못했습니다. 어댑터와 드라이버를 확인하세요."),
        ["err.send_failed"] = ("Bluetooth transmission failed: {0}", "블루투스 송신 실패: {0}"),
        ["err.adapter_unavailable_detail"] = ("The Bluetooth adapter is unavailable: {0}", "블루투스 어댑터를 사용할 수 없습니다: {0}"),
        ["err.not_supported"] = (
            "This PC's Bluetooth adapter or driver doesn't support BLE advertising.",
            "이 PC의 블루투스 어댑터 또는 드라이버는 BLE 광고 송신을 지원하지 않습니다."),
        ["err.radio_unavailable"] = (
            "The Bluetooth adapter is unavailable. Check that Bluetooth is powered on.",
            "블루투스 어댑터를 사용할 수 없습니다. 블루투스 전원을 확인하세요."),
        ["err.disabled_by_policy"] = ("Windows policy has blocked Bluetooth use.", "Windows 정책에서 블루투스 사용을 차단했습니다."),
        ["err.advert_stopped_generic"] = (
            "The Bluetooth advertisement was aborted. Status: {0}, Error: {1}",
            "블루투스 광고가 중단되었습니다. 상태: {0}, 오류: {1}"),
    };

    /// <summary>Look up a string by key in the current language, optionally formatting it.</summary>
    public static string T(string key, params object[] args)
    {
        if (!Map.TryGetValue(key, out var pair)) return key;
        string template = IsKorean ? pair.Ko : pair.En;
        return args.Length == 0 ? template : string.Format(template, args);
    }
}
