Bây giờ team nên thu thập dữ liệu theo đúng thứ tự dưới đây. Không cần thu thập toàn bộ game; chỉ cần làm một vertical slice nhỏ nhưng nhất quán để chứng minh pipeline hoạt động.
1. Chốt phạm vi demo trước
Với **Dragon Kingdom**, chỉ nên chọn 3–4 gameplay workflows:
Workflow
Bug phù hợp để demo
Hero Summon
Trừ currency nhưng không nhận hero
Building Upgrade
Trừ tài nguyên nhưng công trình không nâng cấp
World Map March
Đội quân bị kẹt hoặc march không hoàn tất
Alliance Battle
Disconnect, formation không load, điểm không cập nhật

Khuyến nghị case chính:
Hero Summon consumes currency but returns no heroes.
Case này dễ hiểu, severity rõ, screenshot dễ chụp và duplicate detection dễ trình bày.

2. Thu thập game entity catalog
Đây là danh sách những thực thể mà AI được phép nhận diện.
Cần thu thập
Loại entity
Ví dụ
Screens
Kingdom Home, Hero Summon, Hero Detail
Buildings
Castle, Barracks, Academy
Heroes
Tên hero xuất hiện trong phạm vi demo
Resources
Gold, Gems, Wood, Food
Actions
Ten Pull, Upgrade, March, Join Battle
Game modes
World Map, Alliance Battle
UI states
Loading, Empty Result, Timeout
Error messages
Connection Lost, Server Timeout
Builds
1.2.5, 1.2.6, 1.2.7
Platforms
Android, iOS

File nên tạo
data/game-context/entities.json

Ví dụ:
[
  {
    "id": "SCREEN_HERO_SUMMON",
    "type": "screen",
    "name": "Hero Summon",
    "aliases": ["summon", "gacha", "recruit hero"]
  },
  {
    "id": "ACTION_TEN_PULL",
    "type": "action",
    "name": "Ten Pull",
    "aliases": ["summon x10", "10 pull", "multi summon"]
  }
]

MVP chỉ cần khoảng:
6–8 screens
5–8 buildings
6–10 heroes
10–15 actions
5–10 UI states/errors

3. Thu thập screenshots
Team cần tự chụp screenshot trong các luồng đã chọn.
Mỗi màn hình nên có
Trạng thái bình thường
Trạng thái loading
Trạng thái lỗi
Popup error
Resource trước thao tác
Resource sau thao tác
Kết quả thành công
Kết quả thất bại
Ví dụ với Hero Summon
Cần chụp:
Màn Hero Summon bình thường
Trước khi bấm Ten Pull
Popup xác nhận
Loading summon
Result thành công
Result panel trống
Connection timeout
Currency đã bị trừ
Hero reward hiển thị sai
Screenshot có độ phân giải khác
Số lượng tối thiểu
Nhóm
Số ảnh
Hero Summon
15–20
Building Upgrade
10–15
World Map March
10–15
Alliance Battle
10–15
Tổng
Khoảng 50–65

Không cần 100 ảnh ngay từ đầu. Làm case chính trước với khoảng 15 ảnh.

4. Gắn nhãn từng screenshot
Mỗi ảnh phải có metadata tương ứng. Không nên chỉ lưu ảnh trong thư mục rồi để AI tự hiểu.
Ví dụ:
{
  "image_id": "IMG-SUMMON-001",
  "file_name": "summon_empty_result.png",
  "screen": "Hero Summon",
  "action": "Ten Pull",
  "ui_state": "Empty Result",
  "visible_error": "Connection timed out",
  "visible_resources": {
    "gems": 2200
  },
  "expected_behavior": "Display ten summoned heroes",
  "actual_behavior": "No heroes are displayed",
  "linked_ticket_ids": ["BUG-201"],
  "labels_verified": true
}

Nên lưu tại:
data/screenshots/labels.json


5. Thu thập hoặc tạo player reports
Cần tạo các report mơ hồ giống cách player thực sự báo lỗi.
Ví dụ
Tôi quay 10 lần nhưng không thấy hero nào.

Kim cương bị trừ rồi đứng màn hình.

Summon x10 xong không nhận được gì.

Game bị timeout sau khi nhấn summon.

Các report duplicate không được giống nhau hoàn toàn. Hãy thay đổi:
Ngôn ngữ
Độ dài
Từ địa phương
Chính tả
Thứ tự thông tin
Thiếu hoặc có thêm context
Số lượng tối thiểu
Loại report
Số lượng
Duplicate thật
8–10
Bug mới
5–8
Related nhưng không duplicate
5–8
Thiếu thông tin
3–5
Tổng
Khoảng 20–30


6. Tạo synthetic logs
Vì không có log nội bộ của game, team cần tự định nghĩa một format log nhất quán.
Fields cần có
Timestamp
Session ID
Build
Platform
Device
Current screen
Last action
Resource before/after
Server response
Error code
Relevant entity
Event sequence
Ví dụ:
Timestamp=2026-07-11T14:02:19+07:00
SessionId=SESSION-10021
BuildVersion=1.2.7
Platform=Android
Device=Samsung S22
Screen=HeroSummon
Action=TenPull
CurrencyType=Gem
CurrencyBefore=5200
CurrencyAfter=2200
ExpectedRewardCount=10
ReceivedRewardCount=0
ServerResponse=Timeout
ErrorCode=SUMMON_RESULT_TIMEOUT

Mỗi report crash/lỗi nên có
1 log khớp
Một số log gần giống nhưng khác nguyên nhân
Một số log thiếu field
Một số log có conflict để test
Ví dụ conflict:
Form platform: Android
Log platform: Windows

Hệ thống phải phát hiện mâu thuẫn.

7. Tạo historical ticket dataset
Đây là phần bắt buộc để duplicate detection có ý nghĩa.
Ticket cần có
{
  "ticket_id": "BUG-201",
  "title": "Ten-pull consumes gems but returns no heroes",
  "feature": "Hero Summon",
  "screen": "Hero Summon",
  "action": "Ten Pull",
  "actual_result": "Currency is deducted but reward list is empty",
  "expected_result": "Ten hero rewards should be displayed",
  "error_code": "SUMMON_RESULT_TIMEOUT",
  "build_start": "1.2.5",
  "build_end": "1.2.7",
  "platforms": ["Android"],
  "status": "Open",
  "reproduction_steps": [
    "Open Hero Summon",
    "Select Ten Pull",
    "Confirm summon"
  ]
}

Cấu trúc dataset
Nhóm
Số lượng
Hero Summon tickets
10–15
Building Upgrade
8–10
World Map March
8–10
Alliance Battle
8–10
Tổng
35–45


8. Tạo duplicate families
Đừng tạo ticket ngẫu nhiên. Hãy nhóm chúng thành các “duplicate family”.
Ví dụ Family A
Root ticket:
BUG-201:
Ten-pull consumes gems but returns no heroes.

Incoming reports thuộc family:
Gems disappeared after summoning x10.

Quay 10 lần không nhận được tướng.

Timeout after summon, currency already deducted.

Tất cả đều có:
duplicate_of = BUG-201

Ví dụ Family B
Root ticket:
BUG-202:
Summon animation freezes but rewards are granted.

Ticket này có từ khóa gần giống nhưng không phải duplicate của BUG-201.
Đây là hard negative rất quan trọng.

9. Thu thập game behavior catalog
Để tạo Expected Result có grounding, cần một catalog nhỏ mô tả hành vi đúng.
Ví dụ:
{
  "feature": "Hero Summon",
  "action": "Ten Pull",
  "preconditions": [
    "Player has sufficient gems",
    "Summon banner is active"
  ],
  "expected_behavior": [
    "Deduct the configured gem cost",
    "Return exactly ten rewards",
    "Display the summon result screen"
  ],
  "build_range": "1.2.x"
}

Nếu không có catalog này, Expected Result dễ trở thành nội dung do LLM tự suy đoán.
Cần khoảng
3–5 behavior records cho mỗi workflow
Tổng khoảng 15–20 records

10. Thu thập aliases và từ ngữ player thường dùng
Player có thể dùng nhiều cách gọi khác nhau:
Tên chuẩn
Alias
Hero Summon
summon, gacha, quay tướng
Ten Pull
quay 10, summon x10, multi pull
Gems
kim cương, gem, diamond
Alliance
guild, bang hội, liên minh
March
hành quân, kéo quân, dispatch

Danh sách này giúp entity normalization.
Nên lưu:
data/game-context/aliases.json


11. Tạo ground truth
Mỗi incoming report phải có đáp án đúng để đo metric.
Ví dụ:
{
  "report_id": "REPORT-001",
  "duplicate_label": "duplicate",
  "duplicate_of": "BUG-201",
  "expected_feature": "Hero Summon",
  "expected_action": "Ten Pull",
  "expected_error_code": "SUMMON_RESULT_TIMEOUT",
  "expected_severity": "High",
  "required_evidence": [
    "currency_deducted",
    "zero_rewards"
  ]
}

Không có ground truth thì bạn không thể tính:
Recall@3
Top-1 accuracy
False-positive rate
Evidence-grounding rate

12. Tạo benchmark dataset riêng
Không nên dùng tất cả dữ liệu để vừa chỉnh prompt vừa báo cáo kết quả.
Chia thành:
Dataset
Mục đích
Development set
Chỉnh prompt, weights và parser
Evaluation set
Đo kết quả cuối cùng

Ví dụ:
Development:
- 12 incoming reports
- 25 historical tickets

Evaluation:
- 8 incoming reports
- 15 historical tickets chưa dùng để tune


13. Thu thập dữ liệu đo thời gian QA
Để chứng minh win condition, cần đo hai luồng.
Manual triage
QA hoặc thành viên team:
Mở report
Đọc log
Đọc screenshot
Search historical tickets
Chọn duplicate hoặc new
Ghi lại thời gian.
AI-assisted triage
Mở kết quả AI
Review evidence
Review duplicate candidates
Chọn quyết định
Ghi lại thời gian.
Nên có ít nhất:
2–3 người test
8–10 reports
Cùng một protocol

14. Cấu trúc thư mục dữ liệu đề xuất
dataset/
├── game-context/
│   ├── entities.json
│   ├── aliases.json
│   └── behaviors.json
│
├── screenshots/
│   ├── hero-summon/
│   ├── building-upgrade/
│   ├── world-map/
│   ├── alliance-battle/
│   └── labels.json
│
├── logs/
│   ├── LOG-001.txt
│   ├── LOG-002.txt
│   └── metadata.json
│
├── historical-tickets/
│   └── tickets.json
│
├── incoming-reports/
│   └── reports.json
│
├── ground-truth/
│   ├── duplicate-labels.json
│   └── expected-fields.json
│
└── benchmark/
    ├── development.json
    └── evaluation.json


15. Thứ tự thu thập hợp lý nhất
Ngày đầu tiên
Chọn case Hero Summon.
Chốt entity catalog.
Chụp 10–15 screenshots.
Tạo 10 historical tickets.
Tạo 5 incoming reports.
Tạo 5 logs.
Gắn ground truth.
Sau khi pipeline đầu chạy được
Mở rộng lên 30–40 historical tickets.
Thêm Building Upgrade.
Thêm hard negatives.
Thêm screenshots nhiều trạng thái.
Tạo evaluation set riêng.
Đo benchmark.
Bộ dữ liệu tối thiểu để bắt đầu code ngay
Bạn chỉ cần chuẩn bị:
Dữ liệu
Số lượng ban đầu
Gameplay workflow
1
Screens
2–3
Screenshots
10–15
Historical tickets
10–15
Incoming reports
5–8
Logs
5–8
Duplicate families
3
Hard negatives
2–3
Behavior records
3–5

Đừng đợi có đủ 50 ticket mới code. Hãy làm một case hoàn chỉnh trước:
Hero Summon report
+ screenshot
+ synthetic log
+ game behavior
+ historical tickets
→ grounded repro
→ duplicate BUG-201

Khi luồng này chạy ổn, mới nhân rộng dataset sang các feature khác.

