namespace Slot.Common
{
    /// <summary>
    /// 各種參數設定
    /// </summary>
    public static class Bottom_Define
    {
        public const string MoneyFormat = "#,##0.00";
    }

    /// <summary>
    /// EventManager.StartListening
    /// </summary>
    public static class Bottom_EventAction
    {
        #region 設定按鈕狀態
        public const string SetBottomUiNormal = "set_bottom_ui_normal";
        public const string SetBottomUiLock = "set_bottom_ui_lock";
        public const string SetBottomUiFreeGameEnter = "set_bottom_ui_free_game_enter";
        public const string SetBottomUiFreeGameLeave = "set_bottom_ui_free_game_leave";
        #endregion

        #region UI
        public const string UiLoadingNextPage = "ui_loading_next_page";
        public const string UiLoadingPrevPage = "ui_loading_prev_page";

        public const string UiContinueClick = "ui_continue_click";

        public const string UiBetClick = "ui_bet_click";
        public const string UiBetClickOption = "ui_bet_click_option";
        public const string UiBetMinusClick = "ui_bet_minus_click";
        public const string UiBetPlusClick = "ui_bet_plus_click";

        public const string UiMenuClick = "ui_menu_click";
        public const string UiMenuInfoClick = "ui_menu_info_click";
        public const string UiMenuInfoCloseClick = "ui_menu_info_close_click";
        public const string UiMenuLogClick = "ui_menu_log_click";
        public const string UiMenuVolumeClick = "ui_menu_volume_click";
        public const string UiMenuHomeClick = "ui_menu_home_click";

        public const string UiSpinClick = "ui_spin_click";
        public const string UiSpinLock = "ui_spin_lock";

        public const string UiTurboClick = "ui_turbo_click";
        public const string UiWalletClick = "ui_wallet_click";

        // 浮動記分板
        public const string UiSetWinTemp = "ui_set_win_temp";
        public const string UiAddWinTemp = "ui_add_win_temp";
        public const string UiSettleWinTemp = "ui_settle_win_temp";
        public const string UiSettleWinTempEnd = "ui_settle_win_temp_end";
        public const string UiHideWinTemp = "ui_hide_win_temp";
        public const string UiShowFormula = "ui_show_formula";
        /// <summary>
        /// 浮動計分版演出效果 WinTempEffectEnum
        /// </summary>
        public const string UiSetWinTempEffect = "ui_set_win_temp_effect";
        //

        public const string UiSetWin = "ui_set_win";
        public const string UiSetWinEnd = "ui_set_win_end";
        public const string UiResetWin = "ui_reset_win";
        public const string UiAddWin = "ui_add_win";
        public const string UiAddWinEnd = "ui_add_win_end";
        public const string UiAddWinTweenEnd = "ui_add_win_tween_end";
        public const string UiMarqueeSpecial = "ui_marquee_special";

        public const string UiAllClose = "ui_all_close";

        public const string UiAutoClick = "ui_auto_click";
        public const string UiAutoLongClick = "ui_auto_long_click";
        public const string UiAutoSpinWindowClose = "ui_auto_spin_window_close";
        public const string UiAutoSpinWindowClickCancel = "ui_auto_spin_window_click_cancel";
        public const string UiAutoSpinWindowOptionClickChoose = "ui_auto_spin_window_option_click_choose";
        public const string UiAutoSpinWindowOptionClickPlus = "ui_auto_spin_window_option_click_plus";
        public const string UiAutoSpinWindowOptionClickMinus = "ui_auto_spin_window_option_click_minus";
        public const string UiAutoSpinWindowClickStart = "ui_auto_spin_window_click_start";
        public const string UiAutoSpinStopClick = "ui_auto_spin_stop_click";
        public const string UiAutoSpinWindowOptionClickTotalSpin = "ui_auto_spin_window_option_click_total_spin";
        public const string UiAutoSpinCount = "ui_auto_spin_count";

        public const string UiBuyBonusClick = "ui_buy_bonus_click";
        public const string UiBuyBonusCancel = "ui_buy_bonus_cancel";
        //public const string UiBuyFreeSpinsClick = "ui_buy_free_spins_click";
        //public const string UiBuySuperFreeSpinsClick = "ui_buy_super_free_spins_click";
        public const string UiBuyFreeSpinsOk = "ui_buy_free_spins_ok";
        public const string UiBuyFreeSpinsCancel = "ui_buy_free_spins_cancel";

        public const string UiChangeCurrencySwitchClick = "ui_change_currency_switch_click";
        public const string UiChangeCurrencyCancelClick = "ui_change_currency_cancel_click";

        public const string UiExtraBetClick = "ui_extra_bet_click";
        public const string UiSetTransText = "ui_set_trans_text";
        public const string UiSetVerText = "ui_set_ver_text";
        public const string UiSetEnvText = "ui_set_env_text";

        public const string GameChangeTurbo = "game_change_turbo";
        public const string GameChangeBet = "game_change_bet";
        public const string GameChangeExtrabet = "game_change_extrabet";
        public const string GameChangeBalance = "game_change_balance";
        public const string GameChangeCurrency = "game_change_currency";

        #endregion
    }

    /// <summary>
    /// EventManager.SetData
    /// </summary>
    public static class Bottom_EventData
    {
        public const string AutoSpinTotalSpins = "auto_spin_total_spins";
        public const string AutoSpinSingleWinRatioExceeds = "auto_spin_single_win_ratio_exceeds";
        public const string AutoSpinStopIfBalanceLessThan = "auto_spin_stop_if_balance_less_than";
        public const string AutoSpinStopIfBalanceGreaterThan = "auto_spin_stop_if_balance_greater_than";
        public const string AutoSpinStopIfFreeGameIsActivated = "auto_spin_stop_if_free_game_is_activated";

        public const string GameAuto = "game_auto";

        /// <summary>
        /// 加速 - 遊戲目前設定值
        /// </summary>
        public const string GameTurbo = "game_turbo";
        /// <summary>
        /// 加速 - 玩家設定值
        /// </summary>
        public const string PlayerTurbo = "player_turbo";

        public const string GameBuyType = "game_buy_type";
        public const string GameBetList = "game_bet_list";
        public const string GameCurrentBet = "game_current_bet";
        public const string GameCurrentBalance = "game_current_balance";

        public const string GameIsInFreeGame = "game_is_in_free_game";
        public const string GameFreeGameRoundIndex = "game_free_game_round_index";
        public const string GameFreeGameSpinTempCount = "game_free_game_spin_temp_count";

        public const string UiWinTempType = "ui_win_temp_type";
        public const string UiWinTempValue = "ui_win_temp_value";

        public const string UiCurrentWin = "ui_current_win";
        public const string UiAddWin = "ui_add_win";

        public const string GameIsExtraBet = "game_is_extra_bet";
        public const string GameExtraBetRatio = "game_extra_bet_ratio";

        public const string GameRecordUrl = "game_record_url";
        public const string GameHomeUrl = "game_home_url";
        public const string PlatfromId = "platfrom_id";

        public const string GameCurrencyData = "game_currency_data";
    }

    public static class Bottom_AudioName
    {
        public const string Se_Button = "se_button";
        public const string Se_Buy_Window = "se_buy_window";
        public const string Se_Confirm = "se_confirm";
        public const string Se_Cancel = "se_cancel";
        public const string Se_Regularwin = "se_regularwin";
        public const string Se_Scatter_Ring = "se_scatter_ring";
    }

    public enum Bottom_TurboType
    {
        TurboOff = 0,
        TurboQuick = 1,
    }

    /// <summary>
    /// 運算方式
    /// </summary>
    public enum Bottom_MathType
    {
        /// <summary>
        /// 加法
        /// </summary>
        Addition,
        /// <summary>
        /// 減法
        /// </summary>
        Subtraction,
        /// <summary>
        /// 乘法
        /// </summary>
        Multiplication,
        /// <summary>
        /// 除法
        /// </summary>
        Division
    }
}
