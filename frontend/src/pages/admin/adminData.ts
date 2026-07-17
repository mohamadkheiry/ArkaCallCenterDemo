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
  group: 'openai' | 'sms' | 'limits' | 'rag' | 'bale'
}[] = [
  { key: 'openai.baseUrl', label: 'Base URL', group: 'openai', hint: 'مثلاً https://api.openai.com/v1' },
  { key: 'openai.apiKey', label: 'API Key', group: 'openai', secret: true },
  { key: 'openai.embeddingModel', label: 'مدل Embedding', group: 'openai' },
  { key: 'openai.chatModel', label: 'مدل Chat (برای بررسی محتوا)', group: 'openai' },
  { key: 'openai.realtimeModel', label: 'مدل Realtime', group: 'openai' },
  { key: 'openai.ttsModel', label: 'مدل TTS', group: 'openai' },

  { key: 'smsir.apiKey', label: 'API Key سرویس SMS.ir', group: 'sms', secret: true },
  { key: 'smsir.verifyTemplateId', label: 'شناسه قالب کد تأیید (Template ID)', group: 'sms', hint: 'قالب /send/verify با پارامتر CODE — برای ارسال کد ورود' },
  { key: 'smsir.lineNumber', label: 'شماره خط SMS.ir (پیامک رویدادها)', group: 'sms' },

  { key: 'limits.defaultCallMinutes', label: 'سقف پیش‌فرض مکالمه (دقیقه)', group: 'limits' },
  { key: 'limits.warningPercent', label: 'درصد هشدار نزدیک شدن به سقف', group: 'limits' },

  {
    key: 'rag.similarityThreshold',
    label: 'آستانه شباهت RAG',
    group: 'rag',
    control: 'percentSlider',
    hint: '۱۰۰٪ = دقیق‌ترین شباهت (فقط پاسخ‌های بسیار مرتبط) · ۰٪ = خلاقانه‌ترین حالت',
  },
  { key: 'rag.topK', label: 'تعداد قطعات بازیابی‌شده (topK)', group: 'rag' },

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
