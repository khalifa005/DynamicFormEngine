using KH.Domain.Entities.Fsms.Catalog;
using Microsoft.EntityFrameworkCore;

namespace KH.Infrastructure.Data;

/// <summary>
/// Seeds the FIELD_CATALOG table with standard field definitions for the form builder
/// data-name autocomplete. Upserts by DataName so re-runs are safe.
/// </summary>
internal static class FsmsFieldCatalogSeedData
{
    private static readonly (string DataName, string FieldType, string LabelEn, string LabelAr, string Description)[] Entries =
    [
        // ── Customer / Account ───────────────────────────────────────────────
        ("customer_name",       "text",          "Customer Name",          "اسم العميل",           "Full name of the customer"),
        ("customer_id",         "text",          "Customer ID",            "رقم هوية العميل",      "National ID or Iqama number"),
        ("account_no",          "text",          "Account Number",         "رقم الحساب",           "Billing account number"),
        ("mobile_no",           "text",          "Mobile Number",          "رقم الجوال",           "Primary mobile contact"),
        ("email",               "text",          "Email Address",          "البريد الإلكتروني",    "Customer email address"),

        // ── Address / Location ───────────────────────────────────────────────
        ("address",             "geolocation",   "Address",                "العنوان",              "Map pin + optional formatted address (Fulcrum Address field)"),
        ("building_no",         "text",          "Building Number",        "رقم المبنى",           "National address building number"),
        ("city",                "text",          "City",                   "المدينة",              "City name"),
        ("district",            "text",          "District",               "الحي",                 "District / neighbourhood"),
        ("postal_code",         "text",          "Postal Code",            "الرمز البريدي",        "5-digit Saudi postal code"),
        ("gps_location",        "geolocation",   "GPS Location",           "الموقع الجغرافي",      "Field GPS coordinates"),

        // ── Meter ────────────────────────────────────────────────────────────
        ("meter_no",            "text",          "Meter Number",           "رقم العداد",           "Water meter serial number"),
        ("meter_reading",       "numeric",       "Meter Reading",          "قراءة العداد",         "Current meter reading (m³)"),
        ("meter_status",        "single_choice", "Meter Status",           "حالة العداد",          "Physical condition of the meter"),
        ("meter_size",          "single_choice", "Meter Size",             "حجم العداد",           "Meter size in inches"),
        ("meter_age_years",     "numeric",       "Meter Age (Years)",      "عمر العداد (سنوات)",   "Estimated age of the meter"),
        ("meter_photo",         "photo",         "Meter Photo",            "صورة العداد",          "Photo of the water meter"),

        // ── Inspection / Survey ──────────────────────────────────────────────
        ("inspection_date",     "date",          "Inspection Date",        "تاريخ الكشف",          "Date the field visit took place"),
        ("inspection_time",     "time",          "Inspection Time",        "وقت الكشف",            "Time the field visit took place"),
        ("inspector_name",      "text",          "Inspector Name",         "اسم المفتش",           "Name of the field inspector"),
        ("inspector_id",        "text",          "Inspector ID",           "رقم المفتش",           "Employee ID of the inspector"),
        ("visit_result",        "single_choice", "Visit Result",           "نتيجة الزيارة",        "Outcome of the field visit"),
        ("remarks",             "text",          "Remarks",                "ملاحظات",              "Free-text observations"),
        ("signature",           "signature",     "Customer Signature",     "توقيع العميل",         "Customer acknowledgement signature"),
        ("calendar_with_hours", "calendar_with_hours", "Calendar with Hours", "التقويم مع الساعات",   "Weekly working hours — a from/to pair per day"),

        // ── Leak / Fault ─────────────────────────────────────────────────────
        ("leak_detected",       "yes_no",        "Leak Detected?",         "هل يوجد تسرب؟",        "Yes/No — leak was found"),
        ("leak_location",       "text",          "Leak Location",          "موقع التسرب",          "Description of where the leak is"),
        ("leak_photo",          "photo",         "Leak Photo",             "صورة التسرب",          "Photo evidence of the leak"),
        ("fault_type",          "single_choice", "Fault Type",             "نوع العطل",            "Category of the reported fault"),
        ("fa_type_code",        "text",          "FA Type Code",           "رمز نوع الطلب",        "Field activity type code for this job"),
        ("task_no",             "text",          "Task Number",            "رقم المهمة",           "Task / work order number"),

        // ── Network ──────────────────────────────────────────────────────────
        ("pipe_diameter_mm",    "numeric",       "Pipe Diameter (mm)",     "قطر الأنبوب (مم)",     "Diameter of the water pipe in millimetres"),
        ("pipe_material",       "single_choice", "Pipe Material",          "مادة الأنبوب",         "Material of the water pipe"),
        ("pressure_bar",        "numeric",       "Pressure (Bar)",         "الضغط (بار)",          "Water pressure measured at site"),
        ("valve_status",        "single_choice", "Valve Status",           "حالة الصمام",          "Open / Closed / Partial"),

        // ── Attachment & Evidence ────────────────────────────────────────────
        ("site_photo",          "photo",         "Site Photo",             "صورة الموقع",          "General site photograph"),
        ("before_photo",        "photo",         "Before Photo",           "صورة قبل التدخل",      "Photo before maintenance work"),
        ("after_photo",         "photo",         "After Photo",            "صورة بعد التدخل",      "Photo after maintenance work"),
        ("video_evidence",      "video",         "Video Evidence",         "مقطع مرئي",            "Short video of the issue"),

        // ── Compliance / Safety ──────────────────────────────────────────────
        ("ppe_worn",            "yes_no",        "PPE Worn?",              "هل تم ارتداء معدات الحماية؟", "Personal protective equipment compliance"),
        ("safety_remarks",      "text",          "Safety Remarks",         "ملاحظات السلامة",      "Any safety observations"),
        ("work_completed",      "yes_no",        "Work Completed?",        "هل اكتمل العمل؟",      "Indicates if the job was fully completed"),

        // ── Fulcrum المسح الميداني (SRV-FIELD-SURVEY-001) ────────────────────
        ("area",                "single_choice", "Neighborhoods",          "الاحياء",              "Neighbourhood / district"),
        ("parcel_type",         "single_choice", "Property Type",          "نوع العقار",           "Villa, building, mosque, …"),
        ("consumption_type",    "single_choice", "Consumption Type",       "نوع الاستهلاك",        "Residential / commercial / government"),
        ("spl_building_no",     "numeric",       "Building Number",        "رقم المبني",           "SPL building number"),
        ("parcel_floors_count", "numeric",       "Number of Floors",       "عدد الادوار",          "Storeys on the parcel"),
        ("parcel_units_count",  "numeric",       "Number of Units",        "عدد الوحدات",          "Units on the parcel"),
        ("parcel_photos",       "photo",         "Property Photos",        "صور العقار",           "Photos of the property"),
        ("sewer_connection_exists", "yes_no",    "Sewage connection exists", "يوجد توصيلة صرف",    "Whether a sewer connection is present"),
        ("sewer_connection_photos", "photo",     "Sewage connection photos", "صور توصيلة الصرف الصحي", "Photos of the sewer connection"),
        ("water_connection_exists", "yes_no",    "Water connection exists", "يوجد توصيلة مياه",    "Whether a water connection is present"),
        ("_parcel_working_case", "yes_no",       "Property Status",        "حالة العقار",          "Working / occupied status"),
        ("water_house_connection_type", "single_choice", "Connection Type", "نوع التوصيلة",        "Metered vs direct connection"),
        ("_visible_dc_status",  "single_choice", "Direct connection coordinate", "احداثية الوصلة المباشرة", "Direct-connection visibility / coordinate"),
        ("direct_connection_photos", "photo",    "Direct connection photos", "صور الوصلة المباشرة", "Photos of a direct connection"),
        ("hidden_connection_photos", "photo",    "Hidden connection photos", "صور الوصلة غير ظاهرة", "Photos of a hidden connection"),
        ("parcel_water_meters_count", "numeric", "Number of water meters", "عدد عدادات المياه",    "Count of water meters on the parcel"),
        ("water_meter_status",  "single_choice", "Meter Status",           "حالة العداد",          "Working / removed"),
        ("water_meter_serial_number", "barcode", "Meter Number",           "رقم العداد",           "Scanned water-meter serial"),
        ("water_meter_photos",  "photo",         "Meter Photo",            "صورة العداد",          "Photo of the water meter"),
        ("water_meter_factory_type", "single_choice", "Meter Type",        "نوع العداد",           "Hydrus / Sensus / Widad"),
        ("water_meter_location", "single_choice", "Meter Location",        "موقع العداد",          "e.g. wall-mounted"),
        ("hcn_number",          "numeric",       "Inventory number",       "رقم الحصر",            "HCN / survey inventory number"),
        ("hcn_photo",           "photo",         "Inventory number photos", "صور رقم الحصر",       "Photos of the inventory number"),
        ("demo_plate_number",   "barcode",       "Utility Plate Number",   "رقم لوحة المرافق",     "Utility plate number or scanned QR URL"),
        ("demo_plate_photo",    "photo",         "Utility Plate Photos",   "صور لوحة المرافق",     "Photos of the utility plate"),
        ("number_of_electricity_meters", "numeric", "Number of electricity meters", "عدد عدادات الكهرباء", "Count of electricity meters"),
        ("one_of_electricity_meters_number", "numeric", "Electricity meter number", "رقم عداد الكهرباء", "Electricity meter number"),
        ("electricity_meters_photos", "photo",   "Electricity meter photos", "صور عدادات الكهرباء", "Photos of electricity meters"),
        ("_unregistered_sewer_connection", "yes_no", "Unregistered sewer connection", "وصلة صرف غير مسجلة", "Unregistered drainage connection"),
        ("__unregistered_sewer_connection_photos", "photo", "Unregistered sewer connection photos", "صور وصلة صرف غير مسجلة", "Photos of an unregistered sewer connection"),
        ("___unregistered_sewer_connection_videos", "video", "Unregistered sewer connection video", "فيديو وصلة صرف غير مسجلة", "Video of an unregistered sewer connection"),
        ("comments",            "memo",          "Comments",               "التعليقات",            "Free-text comments"),
        ("_survey",             "yes_no",        "Surveying completed",    "تم الرفع المساحي",     "Whether surveying has been completed"),
        ("task",                "numeric",       "Task",                   "المهمة",               "Task number"),
        ("dma",                 "text",          "DMA",                    "رمز DMA",              "DMA code"),
        ("remarks2",            "memo",          "Additional Remarks",     "ملاحظات إضافية",       "Additional remarks"),

        // ── Microsoft Forms مبادرة المسح الميداني 2026 (SRV-MSFORMS-SURVEY-2026) ─
        // Reused (same type): meter_no, parcel_floors_count, number_of_electricity_meters,
        // electricity_meters_photos, remarks. street_name is now single_choice (Eastern Sector form).
        ("street_name",             "single_choice", "Street name",                "اسم الشارع",              "Main-street segment (Eastern Sector large-building survey)"),
        ("property_name",           "text",          "Property name",              "اسم العقار إن وجد",       "Property name if known"),
        ("meter_photo_indoor",      "photo",         "Indoor meter photo",         "صورة العداد ( الداخلي )", "Photo of the indoor water meter"),
        ("meter_photo_outdoor",     "photo",         "Outdoor meter photo",        "صورة العداد ( الخارجي )", "Photo of the outdoor water meter"),
        ("property_connection_no",  "text",          "Property connection number", "رقم توصيلة العقار",       "House-connection / HCN-style number (may be 'لا يوجد')"),
        ("property_classification", "single_choice", "Property classification",    "تصنيف العقار",            "Commercial / residential / government / other"),
        ("easement_status",         "single_choice", "Easement status",            "حالة الارتفاق",           "Water only / sewage only / water and sewage / other"),
        ("shop_count",              "numeric",       "Number of shops",            "عدد المحلات التجارية",    "Commercial shop count on the parcel"),
        ("property_photo",          "photo",         "Property photo",             "صورة العقار",             "Photo of the property (MS Forms; not Fulcrum parcel_photos)"),
        ("extra_photo",             "photo",         "Extra photo",                "صورة إضافية",             "Optional extra site photo"),
        ("maps_url",                "text",          "Geographic location",        "الموقع الجغرافي",         "Google Maps short URL from Microsoft Forms"),
        ("acknowledgement",         "single_choice", "Acknowledgement",            "التعهد",                  "Surveyor pledge that the entered data is accurate"),

        // ── المسح الميداني نهائي filling-station survey (SRV-STATION-SURVEY-001) ─
        // Reused (same type): city, account_no, meter_no, meter_photo, meter_status,
        // calendar_with_hours, site_photo, sewer_connection_exists, sewer_connection_photos.
        ("cluster", "single_choice", "Cluster", "القطاع", "Organization cluster"),
        ("business_unit", "single_choice", "Business Unit (BU)", "وحدة الأعمال", "Organization business unit"),
        ("station_name_ar", "text", "Station Name in Arabic", "اسم المحطة بالعربية", "Filling-station name in Arabic"),
        ("station_status", "single_choice", "Station Status", "حالة المحطة", "Open / closed / under construction"),
        ("latitude", "numeric", "Location (Latitude)", "الموقع (خط العرض)", "Station latitude"),
        ("longitude", "numeric", "Location (Longitude)", "الموقع (خط الطول)", "Station longitude"),
        ("water_supply_source", "single_choice", "Water Supply Source", "مصدر التغذية", "Primary water supply source"),
        ("hydraulic_operating_method", "single_choice", "Hydraulic Operating Method", "طريقة التشغيل الهيدروليكية", "Primary hydraulic operating method"),
        ("alt_water_supply_source", "single_choice", "Alternative Water Supply Source", "مصدر التغذية البديل", "Alternative water supply source"),
        ("alt_hydraulic_operating_method", "single_choice", "Alternative Hydraulic Operating Method", "طريقة التشغيل الهيدروليكية البديلة", "Alternative hydraulic operating method"),
        ("design_capacity_m3_day", "numeric", "Design Capacity (m³/day)", "الطاقة التصميمية (م³/يوم)", "Design capacity in cubic metres per day"),
        ("daily_distribution_m3_day", "numeric", "Daily Water Distribution Volume (m³/day)", "كمية المياه الموزعة يوميًا (م³/يوم)", "Daily distributed volume"),
        ("tms_integration", "yes_no", "Integration with TMS", "الربط بنظام TMS", "Whether the station is linked to TMS"),
        ("demo_external_integration", "yes_no", "Linked to an external system", "الربط بنظام خارجي", "Whether the station is linked to an external system"),
        ("meter_size_mm", "single_choice", "Meter Size (mm)", "مقاس العداد (ملم)", "Meter size in millimetres (not inches)"),
        ("station_meter_type", "single_choice", "Meter Type", "نوع العداد", "Electronic vs mechanical station meter"),
        ("meter_display_condition", "single_choice", "Meter Display Condition", "حالة شاشة العداد", "Condition of the meter display"),
        ("meter_display_photos", "photo", "Meter Display Photos", "صور شاشة العداد", "Photos of the meter display"),
        ("meter_accessories_condition", "single_choice", "Meter Accessories Condition", "حالة ملحقات العداد", "Condition of meter accessories"),
        ("meter_accessories_photos", "photo", "Meter Accessories Photos", "صور ملحقات العداد", "Photos of meter accessories"),
        ("meter_manufacturer", "single_choice", "Meter Manufacturer", "الشركة المصنعة للعداد", "Station meter manufacturer"),
        ("other_meter_manufacturer", "text", "Other Meter Manufacturer Name", "اسم الشركة المصنعة الأخرى للعداد", "Manufacturer name when Other is selected"),
        ("amr_integration", "yes_no", "Integration with the AMR System", "الربط بنظام AMR", "Whether the meter is linked to AMR"),
        ("signal_device_available", "yes_no", "Signal Transmission Device Available", "وجود جهاز لنقل الإشارة", "Whether a signal-transmission device is present"),
        ("signal_device_photos", "photo", "Signal Transmission Device Photos", "صور جهاز نقل الإشارة", "Photos of the signal-transmission device"),
        ("signal_device_serial", "text", "Signal Transmission Device Serial Number", "الرقم التسلسلي لجهاز نقل الإشارة", "Serial of the signal-transmission device"),
        ("signal_device_type", "single_choice", "Signal Transmission Device Type", "نوع جهاز نقل الإشارة", "RTU / Data Logger / OTHER"),
        ("other_signal_device_type", "text", "Other Signal Transmission Device Type", "نوع جهاز نقل الإشارة الآخر", "Device type when Other is selected"),
        ("communication_status", "single_choice", "Communication Status", "حالة الاتصال", "ONLINE / OFFLINE"),
        ("data_sim_serial", "text", "Data SIM Card Serial Number", "الرقم التسلسلي لشريحة البيانات", "Data SIM serial"),
        ("data_sim_serial_photos", "photo", "Data SIM Card Serial Number Photos", "صور الرقم التسلسلي لشريحة البيانات", "Photos of the data SIM serial"),
        ("is_meter_smart", "yes_no", "Is the Meter Smart?", "هل العداد ذكي؟", "Whether the meter is smart"),
        ("flow_meter_chamber_available", "yes_no", "Flow Meter Chamber Available", "توفر غرفة لعدادات قياس التدفق", "Whether a flow-meter chamber exists"),
        ("flow_meter_chamber_condition", "single_choice", "Flow Meter Chamber Condition", "حالة غرفة عدادات قياس التدفق", "Condition of the flow-meter chamber"),
        ("flow_meter_chamber_photos", "photo", "Flow Meter Chamber Photos", "صور غرفة عدادات قياس التدفق", "Photos of the flow-meter chamber"),
        ("entrance_gate_available", "yes_no", "Entrance Gate Available", "وجود بوابة للدخول", "Whether an entrance gate exists"),
        ("entrance_gate_photos", "photo", "Entrance Gate Photos", "صور لبوابة الدخول", "Photos of the entrance gate"),
        ("entrance_width_m", "numeric", "Entrance Width (m)", "عرض المدخل (م)", "Entrance width in metres"),
        ("electronic_entrance_gate_available", "yes_no", "Electronic Entrance Gate Available", "وجود بوابة إلكترونية للدخول", "Whether the entrance gate is electronic"),
        ("exit_gate_available", "yes_no", "Exit Gate Available", "وجود بوابة للخروج", "Whether an exit gate exists"),
        ("exit_gate_photos", "photo", "Exit Gate Photos", "صور لبوابة الخروج", "Photos of the exit gate"),
        ("exit_width_m", "numeric", "Exit Width (m)", "عرض المخرج (م)", "Exit width in metres"),
        ("electronic_exit_gate_available", "yes_no", "Electronic Exit Gate Available", "وجود بوابة إلكترونية للخروج", "Whether the exit gate is electronic"),
        ("emergency_exits_available", "yes_no", "Emergency Exits Available", "وجود مخارج طوارئ", "Whether emergency exits exist"),
        ("emergency_exits_photos", "photo", "Emergency Exits Photos", "صور لمخارج الطوارئ", "Photos of emergency exits"),
        ("entrance_exit_drainage_available", "yes_no", "Water Drainage at Entrances and Exits Available", "وجود تصريف مياه للمداخل والمخارج", "Drainage at entrances and exits"),
        ("entrance_exit_drainage_photos", "photo", "Entrance and Exit Water Drainage Photos", "صور لوجود تصريف مياه للمداخل والمخارج", "Photos of entrance/exit drainage"),
        ("entrance_exit_drainage_connection", "single_choice", "Entrance and Exit Drainage Connection", "ربط تصريف المياه للمداخل والمخارج", "Where entrance/exit drainage connects"),
        ("other_entrance_exit_drainage_connection", "text", "Other Entrance and Exit Drainage Connection", "جهة الربط الأخرى لتصريف مياه المداخل والمخارج", "Other drainage connection"),
        ("filling_point_drainage_available", "yes_no", "Drainage for Filling Points Available", "وجود تصريف مياه للمناهل", "Drainage at filling points"),
        ("filling_point_drainage_photos", "photo", "Filling Point Drainage Photos", "صور لوجود تصريف مياه للمناهل", "Photos of filling-point drainage"),
        ("filling_point_drainage_connection", "single_choice", "Filling Point Drainage Connection", "ربط تصريف المياه للمناهل", "Where filling-point drainage connects"),
        ("other_filling_point_drainage_connection", "text", "Other Filling Point Drainage Connection", "جهة الربط الأخرى لتصريف مياه المناهل", "Other filling-point drainage connection"),
        ("bilingual_entrance_exit_signs_available", "yes_no", "Bilingual Reflective Entrance/Exit Signs Available", "توفر لوحات عاكسة ثنائية اللغة للمداخل والمخارج", "Bilingual reflective entrance/exit signs"),
        ("bilingual_entrance_exit_signs_photos", "photo", "Bilingual Reflective Entrance/Exit Signs Photos", "صور اللوحات العاكسة ثنائية اللغة للمداخل والمخارج", "Photos of bilingual signs"),
        ("lighting_fixtures_condition", "single_choice", "Lighting Fixtures Condition", "حالة مصابيح الإضاءة", "Condition of lighting fixtures"),
        ("road_signage_condition", "single_choice", "Road Directional Signage Condition", "حالة اللوحات الإرشادية على الطريق", "Condition of road directional signs"),
        ("road_signage_photos", "photo", "Road Directional Signage Photos", "صور اللوحات الإرشادية على الطريق", "Photos of road directional signs"),
        ("road_to_station_condition", "single_choice", "Condition of Road Leading to the Station", "حالة الطريق المؤدي للمحطة", "Condition of the approach road"),
        ("road_to_station_photos", "photo", "Road Leading to the Station Photos", "صور الطريق المؤدي للمحطة", "Photos of the approach road"),
        ("paved_access_road_available", "yes_no", "Paved Access Road Available", "توفر طريق وصول مرصوف إلى المحطة", "Whether a paved access road exists"),
        ("paved_access_road_photos", "photo", "Paved Access Road Photos", "صور طريق الوصول المرصوف إلى المحطة", "Photos of the paved access road"),
        ("road_markings_available", "yes_no", "Road Markings Available", "توفر علامات أرضية على الطريق", "Whether road markings exist"),
        ("internal_directional_signs_available", "yes_no", "Internal Directional Signs Available", "توفر لوحات إرشادية داخل المحطة", "Internal directional signs"),
        ("internal_directional_signs_photos", "photo", "Internal Directional Signs Photos", "صور اللوحات الإرشادية داخل المحطة", "Photos of internal directional signs"),
        ("identification_sign_available", "yes_no", "Identification Sign Available", "توفر لوحة تعريفية للمحطة", "Whether a station identification sign exists"),
        ("identification_sign_photos", "photo", "Identification Sign Photos", "صور اللوحة التعريفية", "Photos of the identification sign"),
        ("identification_sign_compliance", "single_choice", "Identification Sign Compliance with Visual Identity Requirements", "التزام اللوحة التعريفية بمتطلبات الهوية البصرية", "Visual-identity compliance of the identification sign"),
        ("fence_type", "single_choice", "Fence Type", "نوع السور", "Type of station fence"),
        ("fence_photos", "photo", "Fence Photos", "صور للسور", "Photos of the fence"),
        ("fence_overall_condition", "single_choice", "Overall Fence Condition", "الحالة العامة للسور", "Overall fence condition"),
        ("fence_finishes_condition", "single_choice", "Fence Finishes and Paint Condition", "حالة تشطيبات الأسوار ودهاناتها", "Fence finish and paint condition"),
        ("yard_paving_available", "yes_no", "Yard Paving Available", "توفر رصف للساحات", "Whether the yard is paved"),
        ("yard_paving_photos", "photo", "Yard Paving Photos", "صور رصف الساحات", "Photos of yard paving"),
        ("yard_markings_available", "yes_no", "Yard Markings Available", "توفر تخطيط أرضي للساحات", "Whether yard markings exist"),
        ("yard_markings_photos", "photo", "Yard Markings Photos", "صور التخطيط الأرضي للساحات", "Photos of yard markings"),
        ("yard_surface_type", "single_choice", "Yard Surface Type", "نوع سطح الساحة", "Yard surface type"),
        ("small_vehicle_parking_available", "yes_no", "Small-Vehicle Parking Available", "توفر مواقف للمركبات الصغيرة", "Small-vehicle parking"),
        ("small_vehicle_parking_photos", "photo", "Small-Vehicle Parking Photos", "صور مواقف المركبات الصغيرة", "Photos of small-vehicle parking"),
        ("yard_lighting_available", "yes_no", "Yard Lighting Available", "توفر إنارة للساحات", "Yard lighting"),
        ("high_mast_lighting_available", "yes_no", "High-Mast Lighting Available at Entrances and Exits", "توفر أعمدة إنارة عالية عند المداخل والمخارج", "High-mast lighting at gates"),
        ("filling_points_count", "numeric", "Number of Filling Points", "عدد المناهل", "Count of filling points"),
        ("filling_point_photos", "photo", "Filling Point Photos", "صور المناهل", "Photos of filling points"),
        ("operational_filling_points_count", "numeric", "Number of Operational Filling Points", "عدد المناهل العاملة", "Count of operational filling points"),
        ("filling_points_condition", "single_choice", "Filling Points Condition", "حالة المناهل", "Condition of filling points"),
        ("filling_point_height_m", "numeric", "Filling Point Height (m)", "ارتفاع المنهل (م)", "Filling-point height in metres"),
        ("filling_point_diameter_mm", "numeric", "Filling Point Diameter (mm)", "قطر المنهل (ملم)", "Filling-point diameter in millimetres"),
        ("flow_rate_m3_s", "numeric", "Flow Rate (m³/s)", "معدل التدفق (م³/ث)", "Filling-point flow rate"),
        ("pump_flow_rate_m3_s", "numeric", "Pump Flow Rate (m³/s)", "معدل تصريف المضخة (م³/ث)", "Pump discharge rate"),
        ("emergency_filling_points_available", "yes_no", "Are Emergency Filling Points Available?", "هل توجد مناهل للطوارئ؟", "Whether emergency filling points exist"),
        ("emergency_filling_points_count", "numeric", "Number of Emergency Filling Points", "عدد مناهل الطوارئ", "Count of emergency filling points"),
        ("emergency_filling_point_photos", "photo", "Emergency Filling Point Photos", "صور مناهل الطوارئ", "Photos of emergency filling points"),
        ("emergency_filling_points_condition", "single_choice", "Emergency Filling Points Condition", "حالة مناهل الطوارئ", "Condition of emergency filling points"),
        ("emergency_filling_point_height_m", "numeric", "Emergency Filling Point Height (m)", "ارتفاع منهل الطوارئ (م)", "Emergency filling-point height"),
        ("emergency_filling_point_diameter_mm", "numeric", "Emergency Filling Point Diameter (mm)", "قطر منهل الطوارئ (ملم)", "Emergency filling-point diameter"),
        ("filling_point_column_type", "single_choice", "Filling Point Column Type", "نوع عمود المنهل", "Filling-point column type"),
        ("other_filling_point_column_type", "text", "Other Filling Point Column Type", "نوع آخر لعمود المنهل", "Column type when Other is selected"),
        ("filling_point_column_photos", "photo", "Filling Point Column Photos", "صور عمود المنهل", "Photos of the filling-point column"),
        ("filling_point_protection_if_not_concrete", "yes_no", "If the Filling Point Is Not Concrete, Is Protection Provided?", "في حال كان المنهل غير خرساني، هل تتوفر حماية؟", "Protection when the filling point is not concrete"),
        ("curbs_between_filling_points_available", "yes_no", "Are Curbs/Walkways Available Between Filling Points?", "هل توجد أرصفة بين نقاط التعبئة؟", "Curbs between filling points"),
        ("curb_between_filling_points_width_m", "numeric", "Width of Curb/Walkway Between Filling Points (m)", "عرض الرصيف بين نقاط التعبئة (م)", "Width of curb between filling points"),
        ("curb_between_filling_points_photos", "photo", "Curb/Walkway Between Filling Points Photos", "صور الرصيف بين نقاط التعبئة", "Photos of curbs between filling points"),
        ("filling_area_side_stairway_available", "yes_no", "Is a Side Stairway Available at the Filling Area?", "هل يوجد درج جانبي لمنطقة التعبئة", "Side stairway at the filling area"),
        ("filling_area_side_stairway_photos", "photo", "Filling Area Side Stairway Photos", "صور الدرج الجانبي لمنطقة التعبئة", "Photos of the filling-area side stairway"),
        ("filling_area_side_stairway_width_cm", "numeric", "Filling Area Side Stairway Width (cm)", "عرض الدرج الجانبي لمنطقة التعبئة (سم)", "Side-stairway width in centimetres"),
        ("tanker_area_concrete_slabs_available", "yes_no", "Concrete Slabs Available in the Tanker Filling Area", "توفر بلاطات خرسانية في منطقة تعبئة الصهاريج", "Concrete slabs in the tanker filling area"),
        ("tanker_area_concrete_slabs_photos", "photo", "Concrete Slabs in Tanker Filling Area Photos", "صور البلاطات الخرسانية في منطقة تعبئة الصهاريج", "Photos of tanker-area concrete slabs"),
        ("tanker_area_concrete_slabs_condition", "single_choice", "Concrete Slabs Condition in Tanker Filling Area", "حالة البلاطات الخرسانية في منطقة تعبئة الصهاريج", "Condition of tanker-area concrete slabs"),
        ("tanker_area_asphalt_available", "yes_no", "Asphalt Available in the Tanker Filling Area", "توفر أسفلت في منطقة تعبئة الصهاريج", "Asphalt in the tanker filling area"),
        ("tanker_area_asphalt_photos", "photo", "Asphalt in Tanker Filling Area Photos", "صور الأسفلت في منطقة تعبئة الصهاريج", "Photos of tanker-area asphalt"),
        ("filling_area_drainage_slopes_available", "yes_no", "Adequate Drainage Slopes Provided Before and After the Filling Area", "توفر ميول تصريف مناسبة قبل منطقة التعبئة وبعدها", "Drainage slopes around the filling area"),
        ("tanker_parking_available", "yes_no", "Is Tanker Parking Available Inside the Station?", "هل توجد مواقف للصهاريج داخل المحطة؟", "Tanker parking inside the station"),
        ("tanker_parking_photos", "photo", "Tanker Parking Photos", "صور مواقف الصهاريج داخل المحطة", "Photos of tanker parking"),
        ("tanker_parking_spaces_count", "numeric", "Number of Tanker Parking Spaces Inside the Station", "عدد مواقف الصهاريج داخل المحطة", "Count of tanker parking spaces"),
        ("tanker_parking_space_length_m", "numeric", "Tanker Parking Space Length (m)", "طول موقف الصهريج (م)", "Tanker parking space length"),
        ("tanker_parking_space_width_m", "numeric", "Tanker Parking Space Width (m)", "عرض موقف الصهريج (م)", "Tanker parking space width"),
        ("accessible_restrooms_available", "yes_no", "Accessibility-Compliant Restrooms Available", "توفر دورات مياه مهيأة للأشخاص ذوي الإعاقة", "Accessible restrooms"),
        ("accessible_restrooms_photos", "photo", "Accessibility-Compliant Restrooms Photos", "صور دورات المياه المهيأة للأشخاص ذوي الإعاقة", "Photos of accessible restrooms"),
        ("accessible_walkways_available", "yes_no", "Accessible Walkways Available", "توفر ممرات مهيأة للأشخاص ذوي الإعاقة", "Accessible walkways"),
        ("accessible_walkways_photos", "photo", "Accessible Walkways Photos", "صور الممرات المهيأة للأشخاص ذوي الإعاقة", "Photos of accessible walkways"),
        ("accessible_parking_available", "yes_no", "Accessible Parking Spaces Available", "توفر مواقف مخصصة للأشخاص ذوي الإعاقة", "Accessible parking"),
        ("accessible_parking_photos", "photo", "Accessible Parking Spaces Photos", "صور المواقف المخصصة للأشخاص ذوي الإعاقة", "Photos of accessible parking"),
        ("internet_available", "yes_no", "Internet Connectivity Available", "توفر اتصال بالإنترنت", "Whether internet connectivity exists"),
        ("internet_equipment_photos", "photo", "Internet Communication Equipment Photos", "صور معدات الاتصال بالإنترنت", "Photos of internet equipment"),
        ("power_source", "single_choice", "Power Source", "مصدر الطاقة", "Primary power source"),
        ("power_source_photos", "photo", "Power Source Photos", "صور مصدر الطاقة", "Photos of the power source"),
        ("alt_power_sources_available", "yes_no", "Are Alternative Power Sources Available?", "هل تتوفر مصادر بديلة للطاقة؟", "Whether alternative power exists"),
        ("alt_power_source_photos", "photo", "Alternative Power Source Photos", "صور مصادر الطاقة البديلة", "Photos of alternative power sources"),
        ("electricity_meter_no", "text", "Electricity Meter Number", "رقم عداد الكهرباء", "Electricity meter serial (text, not numeric)"),
        ("electricity_meter_number_photos", "photo", "Electricity Meter Number Photos", "صور رقم عداد الكهرباء", "Photos of the electricity meter number"),
        ("tanks_count", "numeric", "Number of Tanks", "عدد الخزانات", "Count of tanks"),
        ("tank_photos", "photo", "Tank Photos", "صور الخزانات", "Photos of tanks"),
        ("storage_capacity_m3", "numeric", "Storage Capacity (m³)", "السعة التخزينية (م³)", "Storage capacity in cubic metres"),
        ("station_customers", "single_choice", "Station Customers", "عملاء المحطة", "Customer mix served by the station"),
        ("admin_building_available", "yes_no", "Administrative Building Available", "توفر مبنى إداري", "Whether an administrative building exists"),
        ("admin_building_photos", "photo", "Administrative Building Photos", "صور المبنى الإداري", "Photos of the administrative building"),
        ("security_guardhouse_available", "yes_no", "Security Guardhouse Available", "توفر مبنى للحراسة الأمنية", "Whether a security guardhouse exists"),
        ("security_guardhouse_photos", "photo", "Security Guardhouse Photos", "صور مبنى الحراسة الأمنية", "Photos of the security guardhouse"),
        ("landscaping_available", "yes_no", "Is Landscaping Available at the Site?", "هل يتوفر تشجير في الموقع؟", "Whether site landscaping exists"),
        ("landscaping_photos", "photo", "Site Landscaping Photos", "صور تشجير الموقع", "Photos of site landscaping"),
        ("filling_area_drainage_network_available", "yes_no", "Is a Drainage Network Available in Filling Areas?", "هل توجد شبكة تصريف في مناطق التعبئة؟", "Drainage network in filling areas"),
        ("filling_area_drainage_network_photos", "photo", "Filling Area Drainage Network Photos", "صور شبكة التصريف في مناطق التعبئة", "Photos of filling-area drainage network"),
        ("reject_waste_loading_bay_available", "yes_no", "Is a Dedicated Loading Bay Available for Reject Water and Chemical Waste?", "هل توجد منطقة تحميل مخصصة للمياه الراجعة والمخلفات الكيميائية؟", "Reject-water / chemical-waste loading bay"),
        ("reject_waste_loading_bay_photos", "photo", "Dedicated Reject Water and Chemical Waste Loading Bay Photos", "صور منطقة تحميل المياه الراجعة والمخلفات الكيميائية", "Photos of the reject-waste loading bay"),
        ("cctv_available", "yes_no", "Are CCTV Cameras Available at the Site?", "هل توجد كاميرات مراقبة في الموقع؟", "Whether CCTV cameras exist"),
        ("cctv_cameras_count", "numeric", "Number of CCTV Cameras at the Site", "عدد كاميرات المراقبة في الموقع", "Count of CCTV cameras"),
        ("cctv_photos", "photo", "Site CCTV Camera Photos", "صور كاميرات المراقبة في الموقع", "Photos of site CCTV cameras"),
    ];

    /// <summary>
    /// <c>address</c> was first seeded as <c>text</c>. The Fulcrum field-survey form stores it as
    /// geolocation; retype the existing row so a later API publish does not 409.
    /// <c>comments</c> / <c>remarks2</c> were seeded as <c>text</c> and are now <c>memo</c>.
    /// <c>street_name</c> was first seeded as <c>text</c> from the MS Forms Excel dump; the Eastern
    /// Sector designer form is a closed street list, so the catalog is now <c>single_choice</c>.
    /// </summary>
    internal static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        foreach (var (dataName, fieldType, labelEn, labelAr, description) in Entries)
        {
            var existing = await context.FieldCatalog
                .FirstOrDefaultAsync(x => x.DataName == dataName, cancellationToken);

            if (existing is not null)
            {
                if (!string.Equals(existing.FieldType, fieldType, StringComparison.OrdinalIgnoreCase))
                {
                    existing.ChangeType(fieldType);
                }

                if (HasPlaceholderArabic(existing))
                {
                    existing.UpdateLabels(labelEn, labelAr, description);
                }

                continue;
            }

            context.FieldCatalog.Add(
                FieldCatalogEntry.Create(dataName, fieldType, labelEn, labelAr, description));
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static bool HasPlaceholderArabic(FieldCatalogEntry existing) =>
        string.IsNullOrWhiteSpace(existing.LabelAr)
        || string.Equals(existing.LabelAr, existing.DataName, StringComparison.OrdinalIgnoreCase)
        || string.Equals(existing.LabelAr, existing.LabelEn, StringComparison.OrdinalIgnoreCase);
}
