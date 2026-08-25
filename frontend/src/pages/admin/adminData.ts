export const SMS_EVENTS: { value: string; label: string }[] = [
  { value: 'OtpRequested', label: 'ارسال کد ورود' },
  { value: 'UserRegistered', label: 'ثبت‌نام کاربر' },
  { value: 'SmartPhoneCreated', label: 'ساخت تلفن هوشمند' },
  { value: 'KnowledgeBaseRejected', label: 'رد پایگاه دانش' },
  { value: 'KnowledgeBaseUpdated', label: 'به‌روزرسانی پایگاه دانش' },
  { value: 'CallLimitNearlyReached', label: 'نزدیک شدن به سقف مکالمه' },
  { value: 'CallLimitReached', label: 'اتمام سقف مکالمه' },
  { value: 'NewCallReceived', label: 'دریافت تماس جدید' },
  { value: 'SystemAlert', label: 'هشدار سیستمی' },
]

export const SETTING_FIELDS: {
  key: string
  label: string
  hint?: string
  secret?: boolean
  control?: 'percentSlider' // اسلایدر ۰ تا ۱۰۰٪ که مقدار ۰ تا ۱ ذخیره می‌کند
  group: 'openai' | 'sms' | 'limits' | 'rag' | 'bale' | 'crm'
}[] = [
  { key: 'openai.baseUrl', label: 'Base URL', group: 'openai', hint: 'مثلاً https://api.openai.com/v1' },
  { key: 'openai.apiKey', label: 'API Key', group: 'openai', secret: true },
  { key: 'openai.chatModel', label: 'مدل پاسخ مستقیم از پایگاه دانش', group: 'openai' },
  { key: 'openai.realtimeModel', label: 'مدل Realtime', group: 'openai' },
  { key: 'openai.ttsModel', label: 'مدل TTS', group: 'openai' },
  { key: 'gapgpt.baseUrl', label: 'GapGPT Base URL', group: 'openai', hint: 'پیش‌فرض https://api.gapgpt.app/v1' },
  { key: 'gapgpt.apiKey', label: 'GapGPT API Key', group: 'openai', secret: true },
  { key: 'gapgpt.cleanerModel', label: 'مدل بازسازی رونوشت', group: 'openai', hint: 'gemini-3.6-flash' },
  { key: 'gapgpt.ttsModel', label: 'مدل اصلی TTS پاسخ‌ها', group: 'openai', hint: 'gemini-2.5-pro-preview-tts' },
  { key: 'gapgpt.ttsVoice', label: 'گوینده Gemini', group: 'openai', hint: 'Kore' },
  { key: 'gapgpt.fallbackTtsModel', label: 'مدل جایگزین TTS', group: 'openai', hint: 'فقط هنگام پاسخ صوتی خالی مسیر Gemini' },
  { key: 'gapgpt.fallbackTtsVoice', label: 'گوینده مسیر جایگزین', group: 'openai', hint: 'alloy' },
  { key: 'whisper.baseUrl', label: 'Whisper Base URL', group: 'openai', hint: 'مثلاً http://192.168.20.189:8101' },
  { key: 'whisper.model', label: 'مدل Whisper', group: 'openai', hint: 'whisper-1' },
  { key: 'whisper.language', label: 'زبان Whisper', group: 'openai', hint: 'fa' },

  { key: 'smsir.apiKey', label: 'API Key سرویس SMS.ir', group: 'sms', secret: true },
  { key: 'smsir.verifyTemplateId', label: 'شناسه قالب کد تأیید (Template ID)', group: 'sms', hint: 'قالب /send/verify با پارامتر CODE — برای ارسال کد ورود' },
  { key: 'smsir.lineNumber', label: 'شماره خط SMS.ir (پیامک رویدادها)', group: 'sms' },

  { key: 'crm.enabled', label: 'فعال بودن ارسال لید به CRM', group: 'crm', hint: 'مقدار true یا false' },
  { key: 'crm.baseUrl', label: 'Base URL سرویس CRM', group: 'crm', hint: 'پیش‌فرض https://api.arkadp.com' },
  { key: 'crm.username', label: 'نام کاربری CRM', group: 'crm' },
  { key: 'crm.password', label: 'رمز عبور CRM', group: 'crm', secret: true },
  { key: 'crm.emailDomain', label: 'دامنه ایمیل جایگزین لیدها', group: 'crm', hint: 'پیش‌فرض demo.arkadp.com' },

  { key: 'limits.defaultCallMinutes', label: 'سقف پیش‌فرض مکالمه (دقیقه)', group: 'limits' },
  { key: 'limits.warningPercent', label: 'درصد هشدار نزدیک شدن به سقف', group: 'limits' },

  // کانال بله — اعلامِ کاربرانِ جدیدِ دمو (حداکثر ۳ پیام برای هر کاربر)
  {
    key: 'bale.enabled',
    label: 'فعال بودن اعلام در کانال بله',
    group: 'bale',
    hint: 'مقدار true یا false — با false هیچ پیامی به کانال ارسال نمی‌شود.',
  },
  {
    key: 'bale.botToken',
    label: 'توکن ربات بله',
    group: 'bale',
    secret: true,
    hint: 'توکنی که BotFatherِ بله می‌دهد. ربات باید ادمینِ کانال باشد تا بتواند پیام بفرستد.',
  },
  {
    key: 'bale.channelId',
    label: 'آی‌دی پابلیک کانال بله',
    group: 'bale',
    hint: 'مثلاً ‎@my_channel — اگر @ را ننویسید خودکار اضافه می‌شود.',
  },
  {
    key: 'bale.baseUrl',
    label: 'Base URL بله',
    group: 'bale',
    hint: 'پیش‌فرض https://tapi.bale.ai — معمولاً نیازی به تغییر نیست.',
  },
]
