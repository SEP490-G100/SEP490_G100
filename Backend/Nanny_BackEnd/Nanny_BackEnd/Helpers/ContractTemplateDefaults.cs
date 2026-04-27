namespace Nanny_BackEnd.Helpers;

public static class ContractTemplateDefaults
{
    public const string DefaultContent = """
[[CENTER]]CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM
[[CENTER]]Độc lập - Tự do - Hạnh phúc
[[CENTER]]HỢP ĐỒNG LAO ĐỘNG TRÔNG GIỮ TRẺ GIA ĐÌNH
Căn cứ Bộ luật Lao động số 45/2019/QH14 được Quốc hội nước Cộng hòa xã hội chủ nghĩa Việt Nam thông qua ngày 20 tháng 11 năm 2019;
Căn cứ Nghị định số 145/2020/NĐ-CP ngày 14 tháng 12 năm 2020 của Chính phủ quy định chi tiết và hướng dẫn thi hành một số điều của Bộ luật Lao động về điều kiện lao động và quan hệ lao động;
Căn cứ vào nhu cầu và khả năng của hai bên.
1. BÊN A: NGƯỜI SỬ DỤNG LAO ĐỘNG (ĐẠI DIỆN GIA ĐÌNH)
Ông/bà: {{ParentName}}
Sinh ngày: {{ParentDOB}}
Số CCCD/CMND/hộ chiếu: {{ParentIdentityNumber}}. Cấp ngày: {{ParentIdentityIssueDate}}. Nơi cấp: {{ParentIdentityIssuePlace}}
Địa chỉ thường trú: {{ParentPermanentAddress}}
Địa chỉ chỗ ở hiện tại: {{ParentCurrentAddress}}
Điện thoại liên hệ: {{ParentPhone}}. Email: {{ParentEmail}}
2. BÊN B: NGƯỜI LAO ĐỘNG (NGƯỜI TRÔNG TRẺ GIA ĐÌNH)
Ông/bà: {{NannyName}}
Sinh ngày: {{NannyDOB}}
Số CCCD/CMND/hộ chiếu: {{NannyIdentityNumber}}. Cấp ngày: {{NannyIdentityIssueDate}}. Nơi cấp: {{NannyIdentityIssuePlace}}
Địa chỉ thường trú: {{NannyPermanentAddress}}
Địa chỉ chỗ ở hiện tại: {{NannyCurrentAddress}}
Điện thoại liên hệ: {{NannyPhone}}
Hai bên cùng nhau thỏa thuận, tự nguyện ký kết Hợp đồng lao động này với các điều khoản và điều kiện chi tiết như sau:
Điều 1. THỜI HẠN HỢP ĐỒNG
1.1. Loại hợp đồng: Hợp đồng lao động xác định thời hạn: {{ContractDurationMonths}} tháng
1.2. Thời hạn: Từ ngày {{StartDate}} đến ngày {{EndDate}}
1.3. Thời gian thử việc (nếu có):
Thời gian thử việc tối đa không quá 06 ngày làm việc đối với lao động gia đình.
Từ ngày {{ProbationStartDate}} đến ngày {{ProbationEndDate}}
Điều 2. ĐỊA ĐIỂM VÀ MÔ TẢ CÔNG VIỆC CHI TIẾT
2.1. Địa điểm làm việc: Tại nhà của Bên A, địa chỉ: {{WorkAddress}}
2.2. Mô tả công việc chi tiết: {{JobDescription}}
Điều 3. MỨC LƯƠNG VÀ CÁC CHẾ ĐỘ KHÁC
3.1. Mức lương chính: {{SalaryAmount}} VNĐ.
Mức lương này áp dụng sau khi Bên B đã đạt yêu cầu thử việc. Mức lương thử việc là 85% mức lương chính: {{ProbationSalaryAmount}} VNĐ.
3.3. Chế độ thưởng và Phụ cấp:
Tiền thưởng Lễ, Tết, Lương tháng thứ 13: Sẽ do Bên A quyết định dựa trên mức độ hoàn thành công việc, thái độ làm việc của Bên B và tình hình tài chính của gia đình.
Phụ cấp đi lại/điện thoại (nếu có): {{AllowanceAmount}} VNĐ/tháng.
3.4. Hình thức và thời hạn trả lương:
Trả lương bằng: Tiền mặt. Chuyển khoản: Chuyển khoản vào Số tài khoản: {{BankAccountNumber}} Ngân hàng: {{BankName}}
Thời hạn trả lương: Trả vào ngày {{SalaryReceivedDate}} hàng tháng. Nếu ngày trả lương trùng vào ngày nghỉ lễ, tết, cuối tuần thì có thể trả trước hoặc sau đó tối đa không quá 03 ngày.
Điều 4. THỜI GIỜ LÀM VIỆC VÀ THỜI GIỜ NGHỈ NGƠI
4.1. Thời giờ làm việc: Theo thỏa thuận giữa hai bên.
4.2. Thời giờ người lao động được nghỉ liên tục trong ngày: Bên B được nghỉ liên tục ít nhất 01 giờ trong ngày làm việc. Thời gian nghỉ ngơi linh hoạt phụ thuộc vào giấc ngủ của Trẻ.
4.3. Nghỉ hằng tuần: Bên B được nghỉ 01 ngày/tuần (thường vào ngày Chủ Nhật). Nếu Bên A có nhu cầu nhờ Bên B làm việc vào ngày nghỉ hằng tuần, phải được sự đồng ý của Bên B và phải trả lương làm thêm giờ bằng 200% đơn giá tiền lương thực trả của ngày làm việc bình thường.
4.4. Nghỉ lễ, tết, nghỉ hằng năm: Áp dụng theo quy định của pháp luật lao động hiện hành.
Điều 5. ĐIỀU KIỆN ĂN Ở, ĐI LẠI (Dành cho người lao động sống tại gia đình)
(Lưu ý: Bỏ qua điều này nếu người lao động làm giờ hành chính không ở lại)
5.1. Chỗ ở: Bên A bố trí cho Bên B chỗ ở hợp vệ sinh, an toàn, có không gian riêng tư tối thiểu. Bên B được sử dụng các tiện ích cơ bản (điện, nước sinh hoạt, wifi) hoàn toàn miễn phí.
5.2. Ăn uống: Bên A phụ trách {{MealPerDay}} bữa ăn/ngày làm việc cùng với gia đình.
5.3. Nghĩa vụ khi lưu trú: Bên B có trách nhiệm giữ gìn vệ sinh chung, tắt các thiết bị điện, nước khi không sử dụng; cung cấp đầy đủ giấy tờ tùy thân hợp lệ để Bên A thực hiện thủ tục đăng ký tạm trú với cơ quan Công an địa phương.
Điều 6. QUYỀN VÀ NGHĨA VỤ CỦA BÊN B (NGƯỜI LAO ĐỘNG)
6.1. Quyền lợi của người lao động:
- Về thanh toán tiền lương, các khoản bảo hiểm y tế, bảo hiểm xã hội, các khoản phụ cấp; thưởng: Được thanh toán đầy đủ, đúng hạn theo Điều 3; các khoản khác thực hiện theo thỏa thuận thực tế (nếu có).
- Được cung cấp môi trường làm việc an toàn, không bị phân biệt đối xử, xúc phạm danh dự, nhân phẩm, quấy rối tình dục hoặc cưỡng bức lao động.
- Được bố trí chỗ ăn, ở khi có thỏa thuận; được bồi thường khi người sử dụng lao động vi phạm cam kết trong hợp đồng.
- Từ chối thực hiện các công việc ngoài phạm vi Hợp đồng này nếu gây nguy hiểm đến tính mạng, sức khỏe hoặc vi phạm pháp luật.
6.2. Nghĩa vụ của người lao động:
- Thực hiện đúng, đầy đủ công việc chăm sóc trẻ và việc liên quan đã thỏa thuận.
- An toàn cho Trẻ là ưu tiên số một: Tuyệt đối không để Trẻ một mình trong các tình huống có thể gây nguy hiểm.
- Tôn trọng nếp sống, văn hóa sinh hoạt của gia đình Bên A. Tuân thủ sự điều hành, hướng dẫn của Bên A trong phương pháp nuôi dạy Trẻ.
- Sử dụng đúng hướng dẫn trang thiết bị, bảo đảm an toàn, phòng cháy chữa cháy và vệ sinh môi trường gia đình.
- Bồi thường theo mức độ lỗi thực tế khi làm mất, hư hỏng tài sản của gia đình Bên A theo quy định pháp luật và thỏa thuận giữa hai bên.
- Cung cấp giấy tờ hợp pháp khi phát sinh nghĩa vụ đăng ký tạm trú.
Điều 7. QUYỀN VÀ NGHĨA VỤ CỦA BÊN A (NGƯỜI SỬ DỤNG LAO ĐỘNG)
7.1. Quyền lợi của người sử dụng lao động:
- Được phân công, giám sát công việc theo hợp đồng nhưng không trái pháp luật và đạo đức xã hội.
- Được yêu cầu bồi thường khi có thiệt hại do lỗi của người lao động theo quy định pháp luật.
7.2. Nghĩa vụ của người sử dụng lao động:
- Thanh toán đúng hạn, đầy đủ các khoản theo Điều 3 và thỏa thuận hợp pháp khác.
- Thực hiện đầy đủ điều kiện ăn, ở theo Điều 5.
- Cung cấp đầy đủ công cụ, dụng cụ làm việc, thực phẩm, thuốc men cần thiết để Bên B hoàn thành nhiệm vụ chăm sóc Trẻ.
- Tôn trọng danh dự, nhân phẩm của Bên B. Không được có hành vi giữ bản chính giấy tờ tùy thân (CCCD/CMND) của Bên B, mà chỉ được giữ bản photo công chứng.
- Thực hiện đăng ký tạm trú theo quy định khi người lao động ở cùng gia đình.
Điều 8. ĐIỀU KHOẢN VỀ BẢO MẬT THÔNG TIN VÀ QUYỀN RIÊNG TƯ
8.1. Bên B cam kết tuyệt đối giữ bí mật mọi thông tin về gia đình Bên A.
8.2. Bên B KHÔNG ĐƯỢC PHÉP tự ý chụp ảnh, quay video Trẻ và gia đình Bên A để đăng tải lên mạng xã hội hoặc gửi cho bên thứ ba khi chưa có sự đồng ý của Bên A.
8.3. Trong thời gian làm việc, Bên B không được tự ý dẫn người lạ, người thân, bạn bè vào nhà Bên A khi chưa được sự đồng ý trước của Bên A.
8.4. Vi phạm điều khoản bảo mật là cơ sở để Bên A đơn phương chấm dứt hợp đồng lao động ngay lập tức và yêu cầu bồi thường thiệt hại (nếu có).
Điều 9. KỶ LUẬT LAO ĐỘNG
- Các trường hợp áp dụng hình thức khiển trách: Đi muộn, nghỉ không báo trước, không tuân thủ quy trình chăm sóc trẻ, vi phạm nội quy đã được nhắc nhở.
- Các trường hợp áp dụng hình thức sa thải: Hành vi bạo hành trẻ, trộm cắp, tiết lộ thông tin riêng tư nghiêm trọng, tái phạm nghiêm trọng sau khi đã xử lý kỷ luật.
Điều 10. CHẤM DỨT HỢP ĐỒNG LAO ĐỘNG
10.1. Đơn phương chấm dứt hợp đồng có báo trước: Mỗi bên có quyền đơn phương chấm dứt hợp đồng lao động nhưng phải báo trước ít nhất 15 ngày đối với hợp đồng có xác định thời hạn (hoặc 30 ngày đối với HĐLĐ không xác định thời hạn).
10.2. Bên A có quyền đơn phương chấm dứt Hợp đồng ngay lập tức và yêu cầu bồi thường nếu Bên B vi phạm một trong các lỗi nghiêm trọng sau:
Có hành vi bạo hành thể chất, bạo hành tinh thần, quát mắng, dọa nạt Trẻ.
Có hành vi trộm cắp, tham ô tài sản của gia đình Bên A.
Bỏ mặc Trẻ trong tình trạng nguy hiểm hoặc tự ý bỏ việc không thông báo từ 02 ngày làm việc trở lên.
Có hành vi cấu kết với người ngoài gây nguy hiểm cho gia đình Bên A.
10.3. Trách nhiệm khi chấm dứt hợp đồng: Hai bên có trách nhiệm thanh toán đầy đủ các khoản tiền liên quan đến quyền lợi của mỗi bên chậm nhất trong thời hạn 14 ngày làm việc kể từ ngày chấm dứt hợp đồng. Bên B phải bàn giao lại toàn bộ tài sản, công cụ làm việc cho Bên A.
Điều 11. BỒI THƯỜNG THIỆT HẠI (NẾU CÓ)
- Các trường hợp người lao động phải bồi thường: Khi gây thiệt hại tài sản do lỗi cố ý hoặc lỗi nặng, theo giá trị thiệt hại thực tế.
- Các trường hợp người sử dụng lao động phải bồi thường: Khi đơn phương vi phạm hợp đồng trái pháp luật hoặc gây thiệt hại quyền, lợi ích hợp pháp của người lao động.
Điều 12. GIẢI QUYẾT TRANH CHẤP
12.1. Nếu phát sinh tranh chấp, hai bên sẽ ưu tiên giải quyết thông qua thương lượng, hòa giải trên tinh thần thiện chí, tôn trọng lẫn nhau.
12.2. Trong trường hợp không thể hòa giải, một trong hai bên có quyền yêu cầu Hòa giải viên lao động hoặc Tòa án nhân dân có thẩm quyền giải quyết theo quy định của pháp luật Việt Nam.
Điều 13. ĐIỀU KHOẢN CHUNG
13.1. Hợp đồng này là toàn bộ thỏa thuận giữa Bên A và Bên B, thay thế cho mọi trao đổi, cam kết trước đây (nếu có) bằng lời nói hay văn bản.
13.2. Mọi sửa đổi, bổ sung đối với Hợp đồng này đều phải được lập thành Phụ lục Hợp đồng bằng văn bản và có chữ ký của hai bên.
13.3. Hợp đồng này có hiệu lực kể từ ngày ký.
13.4. Hợp đồng được lập thành 02 (hai) bản có giá trị pháp lý như nhau, Bên A giữ 01 (một) bản, Bên B giữ 01 (một) bản để thực hiện.
Điều 14. THỎA THUẬN KHÁC (NẾU CÓ):
Hai bên ưu tiên thương lượng khi phát sinh tranh chấp; nếu không đạt thỏa thuận thì yêu cầu cơ quan có thẩm quyền giải quyết theo pháp luật.
(Hai bên đã đọc kỹ, hiểu rõ các điều khoản và tự nguyện ký tên dưới đây)
BÊN SỬ DỤNG LAO ĐỘNG (BÊN A)                                          NGƯỜI LAO ĐỘNG (BÊN B)
(Ký, ghi rõ họ tên)                                                                             (Ký, ghi rõ họ tên)
""";
}
