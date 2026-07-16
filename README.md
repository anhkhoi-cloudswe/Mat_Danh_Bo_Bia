
# 🕵️‍♂️ Mật Danh Bò Bía (Game 3D)

**Mật Danh Bò Bía** là một dự án game nhập vai trinh thám 3D độc đáo được phát triển trên nền tảng **Unity Editor 6000.4.6f1**. Game lấy bối cảnh một khu phố lao động đặc trưng của Việt Nam, nơi người chơi sẽ hóa thân thành anh bán Bò bía lề đường để thực hiện các nhiệm vụ điều tra, thu thập manh mối ngầm và phá giải các vụ án phức tạp.

<img width="1919" height="1040" alt="Main_Menu_Mat_Danh_BoBia" src="https://github.com/user-attachments/assets/0e21b176-dcc0-490d-b02d-ee288d1baccb" />
---

## 🌟 Tính Năng Nổi Bật

### 📅 Hệ Thống Cốt Truyện và Nhiệm Vụ Theo Tuyến Thời Gian
Dự án được thiết kế xoay quanh các giai đoạn cốt truyện chi tiết (Phase 1 & Phase 2), dẫn dắt người chơi qua chuỗi hoạt động ngày đêm chân thực:
*   **Giai Đoạn 1 (Phase 1 - Anh Bò Bía):** Bắt đầu hành trình điều tra đối tượng tình nghi (Huy Sẹo). Người chơi sẽ chứng kiến hoạt cảnh đối tượng bỏ chạy, làm rơi hột quẹt, phi tang vật chứng vào bãi rác và tiến hành nhiệm vụ tìm kiếm manh mối ngầm.
*   **Giai Đoạn 2 (Phase 2 - Mật Phục):** Đóng vai người bán Bò bía vào buổi sáng để tiếp cận các khách hàng quen thuộc trong khu phố (Bà Nga, anh Shipper, Mê Liu), thu thập lời thoại tình nghi trước khi tiến hành mật phục, theo dõi căn biệt thự khả nghi vào ban đêm.

### 🎭 Điện Ảnh Hóa và Cắt Cảnh (Cutscenes & Cinematic)
*   Sử dụng camera điện ảnh để chuyển đổi góc nhìn mượt mà giữa góc nhìn thứ ba điều khiển nhân vật và góc nhìn tự động của hoạt cảnh.
*   Hệ thống hội thoại bóng nói (**Dialogue Bubbles**) và giao diện trò chuyện chi tiết giúp bộc lộ nội tâm nhân vật và diễn biến câu chuyện.

### 🔍 Cơ Chế Trinh Thám & Quản Lý Manh Mối
*   **Evidence HUD / Clue Notification:** Thông báo thời gian thực và quản lý các manh mối đã thu thập được để phục vụ mục đích phá án.
*   **Bán hàng tương tác:** Mini-game làm và bán Bò bía đóng vai trò là vỏ bọc hoàn hảo để tiếp cận nhân chứng và thu thập tin tình báo.

---

## 🛠️ Công Nghệ Sử Dụng

*   **Engine:** Unity 6000.4.6f1
*   **Ngôn ngữ:** C# (State Machine, Coroutines, NavMesh Navigation, New Input System)
*   **Quản lý tài nguyên lớn:** Git LFS được tích hợp để lưu trữ các model 3D nặng (.fbx) và texture chất lượng cao.

---

## 📂 Cấu Trúc Mã Nguồn (Scripts)

Dưới đây là sơ đồ cấu trúc của phần mã nguồn điều khiển cốt lõi (chứa trong thư mục `Assets/Scripts/`):

*   📂 **Managers**: Chứa các lớp quản lý luồng sự kiện chính, thời gian trong game và trạng thái nhiệm vụ (`StoryPhase1Manager.cs`, `StoryPhase2Manager.cs`, `GameTimeManager.cs`, `CustomerManager.cs`, `CaseManager.cs`).
*   📂 **UI**: Điều khiển toàn bộ giao diện người dùng, phụ đề hội thoại, cắt cảnh và hệ thống manh mối (`DialogueScreenUI.cs`, `EvidenceHUD.cs`, `ClueNotificationManager.cs`, `FinalCutsceneController.cs`).
*   📂 **Controllers**: Điều khiển di chuyển, hành vi nhân vật và camera.
*   📂 **Audio** & **Animation**: Đồng bộ âm thanh nền và các chuyển động phức tạp của NPC/Người chơi.

---

## 👥 Đóng Góp Dự Án (Contributors)

* **Anh Khôi (`anhkhoi-cloudswe`) — Project Manager & Main Lead**
    * **Quản lý & Thiết kế:** Điều phối tiến độ, biên kịch kịch bản cốt truyện, dàn dựng các phân cảnh Virtual Camera/Timeline (Cutscenes).
    * **Lập trình:** Phát triển và tối ưu giao diện điều khiển chính (Main Menu).
    * **Mỹ thuật & Môi trường:** Trực tiếp đóng góp phát triển mô hình nhân vật 3D, tinh chỉnh kỹ thuật và bố trí không gian bản đồ (Level Design).
* **Gia Bảo (`hackerviet-dev`) — Lead Gameplay Programmer**
    * **Lập trình:** Xây dựng logic vận hành, xử lý cơ chế tương tác và hệ thống điều khiển chính của trò chơi (Main Gameplay).
* **Bách Khoa (`BachKhoaHL`) — Environment & 3D Artist**
    * **Mỹ thuật:** Khởi tạo kiến trúc khu nhà bản đồ, sắp đặt asset bối cảnh và hoàn thiện môi trường xung quanh (Background).

> [!NOTE]
> Để bảo vệ quyền sở hữu trí tuệ về mặt mỹ thuật và thiết kế màn chơi độc quyền, kho lưu trữ public này **chỉ chia sẻ phần mã nguồn lập trình cốt lõi và tài liệu hướng dẫn**. Các tài nguyên hình ảnh 3D, mô hình nhân vật (Models), các Prefab đã được cấu dựng sẵn, hiệu ứng Shader và âm thanh gốc (Soundtrack) đã được lược bỏ khỏi nhánh này.
