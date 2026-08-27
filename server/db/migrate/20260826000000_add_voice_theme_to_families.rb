class AddVoiceThemeToFamilies < ActiveRecord::Migration[8.1]
  def change
    add_column :families, :voice_theme, :string, null: false, default: "stella"
  end
end
